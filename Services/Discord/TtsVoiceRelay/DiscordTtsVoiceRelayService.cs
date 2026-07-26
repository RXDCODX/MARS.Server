using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.Discord.Gateway;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
#if !USE_LOCAL_DSHARPPLUS_VOICE
using DSharpPlus.VoiceNext;
#else
using DSharpPlus.Voice;
#endif

namespace MARS.Server.Services.Discord.TtsVoiceRelay;

public class DiscordTtsVoiceRelayService(
    IDiscordGatewayService gatewayService,
    Services.Twitch.Synthesizer.ITtsHubBroadcaster ttsHubBroadcaster,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<DiscordTtsVoiceRelayService> logger
) : IDiscordTtsVoiceRelayService, IHostedService
{
    private ulong _targetDiscordUserId;
    private ulong _targetDiscordVoiceChannelId;

    private readonly SemaphoreSlim _playbackLock = new(1, 1);
    private readonly SemaphoreSlim _stateLock = new(1, 1);

#if USE_LOCAL_DSHARPPLUS_VOICE
    private VoiceConnection? _voiceConnection;
#else
    private VoiceNextConnection? _voiceConnection;
#endif

    public bool IsVoiceRoutingEnabled { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await LoadConfigurationAsync(cancellationToken);
        gatewayService.RegisterVoiceStateUpdatedHandler(HandleVoiceStateUpdatedAsync);
        await RefreshRoutingStateAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            IsVoiceRoutingEnabled = false;
            await BroadcastRelayStateAsync(false, cancellationToken);

#if USE_LOCAL_DSHARPPLUS_VOICE
            await DisconnectVoiceIfConnectedAsync();
#else
            DisconnectVoiceIfConnected();
#endif
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task HandleRelayedAudioAsync(
        byte[] pcmAudio,
        int sampleRate,
        int channels,
        string text,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsVoiceRoutingEnabled || pcmAudio.Length == 0)
        {
            return;
        }

        await _playbackLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await EnsureVoiceConnectionAsync(cancellationToken);
            if (connection is not null)
            {
#if USE_LOCAL_DSHARPPLUS_VOICE
                var writer = connection.CreateAudioWriter(AudioFormat.S16LE48KHzStereoPCM);
                await writer.WriteAsync(pcmAudio, cancellationToken);
                await writer.FlushAsync(cancellationToken);
                writer.SignalSilence();
#else
                var transmitSink = connection.GetTransmitSink(20);
                await connection.SendSpeakingAsync(true);
                await transmitSink.WriteAsync(pcmAudio, 0, pcmAudio.Length, cancellationToken);
                await transmitSink.FlushAsync(cancellationToken);
                await connection.WaitForPlaybackFinishAsync();
                await connection.SendSpeakingAsync(false);
#endif
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка воспроизведения relayed audio в Discord voice");
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private async Task LoadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var userIdEntry = await dbContext.RootState.FirstOrDefaultAsync(
            e => e.Name == RootStateKeys.DiscordTtsRelayTargetUserId,
            cancellationToken
        );
        var channelIdEntry = await dbContext.RootState.FirstOrDefaultAsync(
            e => e.Name == RootStateKeys.DiscordTtsRelayTargetVoiceChannelId,
            cancellationToken
        );

        if (userIdEntry is not null && ulong.TryParse(userIdEntry.Value, out var userId))
        {
            _targetDiscordUserId = userId;
        }

        if (channelIdEntry is not null && ulong.TryParse(channelIdEntry.Value, out var channelId))
        {
            _targetDiscordVoiceChannelId = channelId;
        }

        logger.LogInformation(
            "Discord TTS Relay config loaded: UserId={UserId}, ChannelId={ChannelId}",
            _targetDiscordUserId,
            _targetDiscordVoiceChannelId
        );
    }

    private async Task HandleVoiceStateUpdatedAsync(
        DiscordClient client,
        VoiceStateUpdatedEventArgs args
    )
    {
        if (args.UserId != _targetDiscordUserId)
        {
            return;
        }

        var userJoinedTargetChannel = args.After?.ChannelId == _targetDiscordVoiceChannelId;
        var userLeftTargetChannel =
            args.Before?.ChannelId == _targetDiscordVoiceChannelId
            && args.After?.ChannelId != _targetDiscordVoiceChannelId;

        await _stateLock.WaitAsync();
        try
        {
            if (userJoinedTargetChannel)
            {
                logger.LogInformation(
                    "Пользователь зашёл в голосовой канал {ChannelId}, подключаю бота",
                    _targetDiscordVoiceChannelId
                );
                var connection = await EnsureVoiceConnectionAsync();
                if (connection is not null)
                {
                    IsVoiceRoutingEnabled = true;
                    await BroadcastRelayStateAsync(true);
                }
                else
                {
                    logger.LogWarning(
                        "Не удалось подключиться к голосовому каналу {ChannelId}",
                        _targetDiscordVoiceChannelId
                    );
                }
            }
            else if (userLeftTargetChannel)
            {
                logger.LogInformation("Пользователь вышел из голосового канала, отключаю бота");
                IsVoiceRoutingEnabled = false;
                await BroadcastRelayStateAsync(false);
#if USE_LOCAL_DSHARPPLUS_VOICE
                await DisconnectVoiceIfConnectedAsync();
#else
                DisconnectVoiceIfConnected();
#endif
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task BroadcastRelayStateAsync(
        bool relayToDiscord,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var state = new Hubs.Models.VoiceRecognition.TtsState
            {
                RelayToDiscord = relayToDiscord,
                Volume = ttsHubBroadcaster.CurrentVolume,
                IsStopped = false,
            };
            await ttsHubBroadcaster.BroadcastStateAsync(state, cancellationToken);

            logger.LogInformation(
                "Discord relay state broadcast: RelayToDiscord={RelayToDiscord}",
                relayToDiscord
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка broadcastа Discord relay state");
        }
    }

    private async Task RefreshRoutingStateAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var shouldEnable = await IsTargetUserInTargetChannelAsync(cancellationToken);

            if (shouldEnable)
            {
                var connection = await EnsureVoiceConnectionAsync(cancellationToken);
                if (connection is not null)
                {
                    IsVoiceRoutingEnabled = true;
                    await BroadcastRelayStateAsync(true, cancellationToken);
                }
                else
                {
                    IsVoiceRoutingEnabled = false;
                    await BroadcastRelayStateAsync(false, cancellationToken);
                }
            }
            else
            {
                IsVoiceRoutingEnabled = false;
                await BroadcastRelayStateAsync(false, cancellationToken);
#if USE_LOCAL_DSHARPPLUS_VOICE
                await DisconnectVoiceIfConnectedAsync();
#else
                DisconnectVoiceIfConnected();
#endif
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обновления состояния Discord TTS routing");
            IsVoiceRoutingEnabled = false;
            await BroadcastRelayStateAsync(false, cancellationToken);
#if USE_LOCAL_DSHARPPLUS_VOICE
            await DisconnectVoiceIfConnectedAsync();
#else
            DisconnectVoiceIfConnected();
#endif
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task<bool> IsTargetUserInTargetChannelAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = false;

        var client = gatewayService.Client;
        if (client is not null)
        {
            var channel = client
                .Guilds.Values.Select(guild =>
                    guild.Channels.GetValueOrDefault(_targetDiscordVoiceChannelId)
                )
                .FirstOrDefault(channel => channel is not null);

            if (channel is not null)
            {
                result = channel.Users.Any(x => x.Id == _targetDiscordUserId);
            }
        }

        return result;
    }

#if USE_LOCAL_DSHARPPLUS_VOICE
    private async Task<VoiceConnection?> EnsureVoiceConnectionAsync(
#else
    private async Task<VoiceNextConnection?> EnsureVoiceConnectionAsync(
#endif
        CancellationToken cancellationToken = default
    )
    {
#if USE_LOCAL_DSHARPPLUS_VOICE
        VoiceConnection? result = _voiceConnection;
#else
        VoiceNextConnection? result = _voiceConnection;
#endif

        if (result is null)
        {
            var client = gatewayService.Client;
            if (client is not null)
            {
                var channel = client
                    .Guilds.Values.Select(guild =>
                        guild.Channels.GetValueOrDefault(_targetDiscordVoiceChannelId)
                    )
                    .FirstOrDefault(channel => channel is not null);

                if (channel is not null)
                {
                    try
                    {
                        result = await channel.ConnectAsync();
                        _voiceConnection = result;

#if USE_LOCAL_DSHARPPLUS_VOICE
                        result.SetDisconnectHandler(
                            async (reason, state) =>
                            {
                                logger.LogWarning(
                                    "Discord voice connection lost: {Reason}",
                                    reason
                                );
                                await _stateLock.WaitAsync();
                                try
                                {
                                    _voiceConnection = null;
                                    if (IsVoiceRoutingEnabled)
                                    {
                                        _voiceConnection = await EnsureVoiceConnectionAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, "Ошибка переподключения к Discord voice");
                                }
                                finally
                                {
                                    _stateLock.Release();
                                }
                            }
                        );
#endif

                        logger.LogInformation(
                            "Discord бот подключился к voice каналу {ChannelId}",
                            _targetDiscordVoiceChannelId
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Ошибка подключения к Discord voice каналу");
                    }
                }
            }
        }

        return result;
    }

#if USE_LOCAL_DSHARPPLUS_VOICE
    private async Task DisconnectVoiceIfConnectedAsync()
    {
        if (_voiceConnection is not null)
        {
            try
            {
                await _voiceConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка отключения от Discord voice канала");
            }
            finally
            {
                _voiceConnection = null;
            }
        }
    }
#else
    private void DisconnectVoiceIfConnected()
    {
        if (_voiceConnection is not null)
        {
            try
            {
                _voiceConnection.Disconnect();
                _voiceConnection.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка отключения от Discord voice канала");
            }
            finally
            {
                _voiceConnection = null;
            }
        }
    }
#endif
}
