using System.Runtime.Versioning;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using MARS.Server.Services.Discord.Gateway;

namespace MARS.Server.Services.Discord.TtsVoiceRelay;

public class DiscordTtsVoiceRelayService(
    IDiscordGatewayService gatewayService,
    ILogger<DiscordTtsVoiceRelayService> logger
) : IDiscordTtsVoiceRelayService, IHostedService
{
    private const ulong TargetDiscordUserId = 260383142903414785;
    private const ulong TargetDiscordVoiceChannelId = 1406679380369080481;

    private readonly SemaphoreSlim _playbackLock = new(1, 1);
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private VoiceNextConnection? _voiceConnection;

    public bool IsVoiceRoutingEnabled { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        gatewayService.RegisterVoiceStateUpdatedHandler(HandleVoiceStateUpdatedAsync);
        await RefreshRoutingStateAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            IsVoiceRoutingEnabled = false;
            DisconnectVoiceIfConnected();
        }
        finally
        {
            _stateLock.Release();
        }
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
                    var transmitSink = connection.GetTransmitSink(20);
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
            await RefreshRoutingStateAsync();
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
            var channel = await client.GetChannelAsync(TargetDiscordVoiceChannelId);
            if (channel is not null)
            {
                result = channel.Users.Any(x => x.Id == TargetDiscordUserId);
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
                var channel = await client.GetChannelAsync(TargetDiscordVoiceChannelId);
                if (channel is not null)
                {
                    result = await channel.ConnectAsync();
                    _voiceConnection = result;
                    logger.LogInformation(
                        "Discord бот подключился к voice каналу {ChannelId}",
                        TargetDiscordVoiceChannelId
                    );
                }
            }
        }

        return result;
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
        var offset = 12;

        while (offset + 8 <= source.Length)
        {
            var chunkId = BitConverter.ToInt32(source, offset);
            var chunkSize = BitConverter.ToInt32(source, offset + 4);

            if (chunkId == 0x61746164)
            {
                result = offset;
                break;
            }

            offset += 8 + chunkSize;
            if (offset % 2 != 0)
            {
                offset++;
            }
        }

        return result;
    }
}