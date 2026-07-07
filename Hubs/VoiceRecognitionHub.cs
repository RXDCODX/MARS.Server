using System;
using System.Threading.Tasks;
using MARS.Server.Hubs.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs;

/// <summary>
/// SignalR hub for TTS delivery to AudioController.
///
/// AudioController connects as a consumer client and receives TTS playback requests.
///
/// The route is preserved for compatibility while the semantics now belong to TTS.
/// </summary>
[AllowAnonymous]
[SignalRHub("/hubs/tts", AutoDiscover.MethodsAndParams)]
public class VoiceRecognitionHub(ILogger<VoiceRecognitionHub> logger) : Hub<IVoiceRecognitionHub>
{
    private const string TtsConsumersGroupName = "tts-consumers";

    /// <summary>
    /// Register the current connection as a TTS consumer.
    /// </summary>
    public async Task RegisterAsTtsConsumer()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, TtsConsumersGroupName);

        logger.LogInformation("TTS consumer registered: {ConnectionId}", Context.ConnectionId);
    }

    /// <summary>
    /// Unregister the current connection from the TTS consumer group.
    /// </summary>
    public async Task UnregisterAsTtsConsumer()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TtsConsumersGroupName);

        logger.LogInformation("TTS consumer unregistered: {ConnectionId}", Context.ConnectionId);
    }

    /// <summary>
    /// Report that TTS playback has started on the consumer.
    /// </summary>
    public Task ReportTtsPlaybackStarted(string text)
    {
        logger.LogInformation(
            "TTS playback started by consumer {ConnectionId}: {Text}",
            Context.ConnectionId,
            text
        );

        return Task.CompletedTask;
    }

    /// <summary>
    /// Report that TTS playback has completed on the consumer.
    /// </summary>
    public Task ReportTtsPlaybackCompleted(string text, TimeSpan duration)
    {
        logger.LogInformation(
            "TTS playback completed by consumer {ConnectionId}: {Text}, duration={Duration}",
            Context.ConnectionId,
            text,
            duration
        );

        return Task.CompletedTask;
    }

    /// <summary>
    /// Report that TTS playback has failed on the consumer.
    /// </summary>
    public Task ReportTtsPlaybackFailed(string text, string error)
    {
        logger.LogWarning(
            "TTS playback failed on consumer {ConnectionId}: {Text}, error={Error}",
            Context.ConnectionId,
            text,
            error
        );

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a connection is established.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected to TtsHub: {ConnectionId}", Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a connection is closed.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TtsConsumersGroupName);

        logger.LogInformation(
            "Client disconnected from TtsHub: {ConnectionId}",
            Context.ConnectionId
        );

        await base.OnDisconnectedAsync(exception);
    }
}
