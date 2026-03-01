using System.Runtime.Versioning;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;

namespace MARS.Server.Services.Discord;

public class DiscordTtsVoiceRelayService(
    IDiscordGatewayService gatewayService,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<DiscordTtsVoiceRelayService> logger
) : BackgroundService, IDiscordTtsVoiceRelayService
{
    private const ulong TargetDiscordUserId = 260383142903414785;
    private const ulong TargetDiscordVoiceChannelId = 1406679380369080481;
    private const ulong TargetDiscordGuildId = 1367222429772021853;

    private readonly SemaphoreSlim _playbackLock = new(1, 1);
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private int _isRetryRefreshScheduled;

    private VoiceNextConnection? _voiceConnection;

    public bool IsVoiceRoutingEnabled { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            logger.LogWarning(
                "Операционная система не поддерживается, Discord TTS routing отключён"
            );
            return;
        }

        hostApplicationLifetime.ApplicationStopping.Register(HandleApplicationStopping);
        gatewayService.RegisterVoiceStateUpdatedHandler(HandleVoiceStateUpdatedAsync);

        try
        {
            await RefreshRoutingStateAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Discord TTS routing startup cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при инициализации Discord TTS routing");
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void HandleApplicationStopping()
    {
        IsVoiceRoutingEnabled = false;
        DisconnectVoiceIfConnected();
    }

    public async Task PlaySpeechAsync(
        string voiceName,
        string text,
        string? additionalText = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!IsVoiceRoutingEnabled)
        {
            return;
        }

        await _playbackLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await EnsureVoiceConnectionAsync(cancellationToken);
            if (connection is not null)
            {
                var pcm = SynthesizeToPcm(voiceName, text, additionalText);
                if (pcm.Length > 0)
                {
                    var transmitSink = connection.GetTransmitSink(text.Length * 1000);
                    await connection.SendSpeakingAsync(true);
                    await transmitSink.WriteAsync(pcm, 0, pcm.Length, cancellationToken);
                    await transmitSink.FlushAsync(cancellationToken);
                    await connection.WaitForPlaybackFinishAsync();
                    await connection.SendSpeakingAsync(false);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка отправки TTS в Discord voice");
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private async Task HandleVoiceStateUpdatedAsync(
        DiscordClient client,
        VoiceStateUpdatedEventArgs args
    )
    {
        if (args.UserId == TargetDiscordUserId)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await RefreshRoutingStateAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ошибка фонового обновления состояния Discord TTS routing");
                }
            });
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
                IsVoiceRoutingEnabled = true;
                await EnsureVoiceConnectionAsync(cancellationToken);
            }
            else
            {
                IsVoiceRoutingEnabled = false;
                DisconnectVoiceIfConnected();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обновления состояния Discord TTS routing");
            IsVoiceRoutingEnabled = false;
            DisconnectVoiceIfConnected();
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
            var guild = await client.GetGuildAsync(TargetDiscordGuildId);
            var channel = await guild.GetChannelAsync(TargetDiscordVoiceChannelId);
            if (channel.Type == DiscordChannelType.Voice)
            {
                result = channel.Users.Any(x => x.Id == TargetDiscordUserId);
            }
        }

        return result;
    }

    private async Task<bool> IsBotInTargetChannelAsync(CancellationToken cancellation = default)
    {
        var result = false;
        var client = gatewayService.Client;
        if (client is not null && _voiceConnection != null)
        {
            var guild = await client.GetGuildAsync(TargetDiscordGuildId);
            var channel = await guild.GetChannelAsync(TargetDiscordVoiceChannelId);
            if (channel.Type == DiscordChannelType.Voice)
            {
                result = channel.Users.Any(x => x.Id == client.CurrentUser.Id);
            }
        }

        return result;
    }

    private async Task<VoiceNextConnection?> EnsureVoiceConnectionAsync(
        CancellationToken cancellationToken = default
    )
    {
        VoiceNextConnection? result = _voiceConnection;

        if (result is null)
        {
            var client = gatewayService.Client;
            if (client is not null)
            {
                var guild = await client.GetGuildAsync(TargetDiscordGuildId);
                var channel = await guild.GetChannelAsync(TargetDiscordVoiceChannelId);
                if (channel.Type == DiscordChannelType.Voice)
                {
                    var botMember = guild.CurrentMember;
                    if (botMember is null)
                    {
                        try
                        {
                            _ = await guild.GetMemberAsync(client.CurrentUser.Id, true);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(
                                ex,
                                "Не удалось получить member-объект бота для guild {GuildId}",
                                TargetDiscordGuildId
                            );
                        }

                        botMember = guild.CurrentMember;
                    }

                    if (
                        botMember is not null
                        && !await IsBotInTargetChannelAsync(cancellationToken)
                    )
                    {
                        try
                        {
                            result = await channel
                                .ConnectAsync()
                                .WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
                            _voiceConnection = result;
                            logger.LogInformation(
                                "Discord бот подключился к voice каналу {ChannelId}",
                                TargetDiscordVoiceChannelId
                            );
                        }
                        catch (TimeoutException ex)
                        {
                            logger.LogWarning(
                                ex,
                                "Таймаут подключения к Discord voice каналу {ChannelId}; будет повтор через 3 секунды",
                                TargetDiscordVoiceChannelId
                            );
                            ScheduleRefreshRetry();
                        }
                    }
                    else
                    {
                        logger.LogWarning(
                            "Пропущено подключение к voice каналу {ChannelId}: bot member недоступен или бот уже в канале",
                            TargetDiscordVoiceChannelId
                        );
                    }
                }
            }
        }

        return result;
    }

    private void ScheduleRefreshRetry()
    {
        if (Interlocked.Exchange(ref _isRetryRefreshScheduled, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                await RefreshRoutingStateAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка retry обновления Discord TTS routing");
            }
            finally
            {
                Interlocked.Exchange(ref _isRetryRefreshScheduled, 0);
            }
        });
    }

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

    [SupportedOSPlatform("windows")]
    private static byte[] SynthesizeToPcm(string voiceName, string text, string? additionalText)
    {
        byte[] result = [];

        if (!string.IsNullOrWhiteSpace(text))
        {
            using var speech = new SpeechSynthesizer();
            using var audioStream = new MemoryStream();

            var formatInfo = new SpeechAudioFormatInfo(
                48000,
                AudioBitsPerSample.Sixteen,
                AudioChannel.Stereo
            );

            speech.SetOutputToAudioStream(audioStream, formatInfo);
            if (!string.IsNullOrWhiteSpace(voiceName))
            {
                speech.SelectVoice(voiceName);
            }

            speech.Speak(text);
            if (!string.IsNullOrWhiteSpace(additionalText))
            {
                speech.Speak(additionalText);
            }

            var bytes = audioStream.ToArray();
            result = ExtractPcmPayload(bytes);
        }

        return result;
    }

    private static byte[] ExtractPcmPayload(byte[] source)
    {
        var result = source;

        if (
            source.Length > 44
            && source[0] == 'R'
            && source[1] == 'I'
            && source[2] == 'F'
            && source[3] == 'F'
        )
        {
            var dataChunkOffset = FindDataChunkOffset(source);
            if (dataChunkOffset >= 0)
            {
                var dataLength = BitConverter.ToInt32(source, dataChunkOffset + 4);
                var payloadOffset = dataChunkOffset + 8;

                if (payloadOffset + dataLength <= source.Length && dataLength > 0)
                {
                    result = new byte[dataLength];
                    Buffer.BlockCopy(source, payloadOffset, result, 0, dataLength);
                }
            }
        }

        return result;
    }

    private static int FindDataChunkOffset(byte[] source)
    {
        var result = -1;

        for (var i = 12; i + 8 < source.Length; i++)
        {
            if (
                source[i] == 'd'
                && source[i + 1] == 'a'
                && source[i + 2] == 't'
                && source[i + 3] == 'a'
            )
            {
                result = i;
                break;
            }
        }

        return result;
    }
}
