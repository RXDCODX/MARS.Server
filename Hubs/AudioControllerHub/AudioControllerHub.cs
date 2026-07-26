using System;
using System.Threading.Tasks;
using MARS.Server.Services.Discord.TtsVoiceRelay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs.AudioControllerHub;

/// <summary>
/// SignalR hub for unified communication with AudioController.
/// Replaces REST calls for SoundBar, OBS, and Health.
/// Merges TTS functionality from VoiceRecognitionHub.
/// </summary>
[AllowAnonymous]
[SignalRHub("/hubs/audio-controller", AutoDiscover.MethodsAndParams)]
public class AudioControllerHub(
    AudioControllerCommandTracker tracker,
    IDiscordTtsVoiceRelayService discordRelayService,
    ILogger<AudioControllerHub> logger
) : Hub<IAudioControllerHub>
{
    private const string GroupName = "audio-controllers";

    /// <summary>
    /// Register the current connection as an AudioController.
    /// </summary>
    public async Task RegisterAsAudioController()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
        logger.LogInformation("AudioController registered: {ConnectionId}", Context.ConnectionId);
    }

    /// <summary>
    /// Response handler — called by AudioController when a command completes.
    /// </summary>
    public Task CommandResponse(string correlationId, bool success, string? data, string? error)
    {
        tracker.TryComplete(
            correlationId,
            new AudioControllerResponse
            {
                CorrelationId = correlationId,
                Success = success,
                Data = data,
                Error = error,
            }
        );
        return Task.CompletedTask;
    }

    /// <summary>
    /// Receive generated PCM audio from AudioController and play it in Discord voice channel.
    /// </summary>
    public async Task SubmitAudioForRelay(
        byte[] pcmAudio,
        int sampleRate,
        int channels,
        string text
    )
    {
        logger.LogInformation(
            "Received audio for relay: {Text}, {SampleRate}Hz, {Channels}ch, {Size}B",
            text,
            sampleRate,
            channels,
            pcmAudio.Length
        );
        await discordRelayService.HandleRelayedAudioAsync(pcmAudio, sampleRate, channels, text);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation(
            "Client connected to AudioControllerHub: {ConnectionId}",
            Context.ConnectionId
        );
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName);
        logger.LogInformation(
            "Client disconnected from AudioControllerHub: {ConnectionId}",
            Context.ConnectionId
        );
        await base.OnDisconnectedAsync(exception);
    }
}
