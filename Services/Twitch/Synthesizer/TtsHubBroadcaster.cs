using MARS.Server.Hubs.Interfaces;
using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class TtsHubBroadcaster(
    IHubContext<Hubs.VoiceRecognitionHub, IVoiceRecognitionHub> hubContext,
    ILogger<TtsHubBroadcaster> logger
)
{
    private const string TtsConsumersGroupName = "tts-consumers";

    public async Task BroadcastAsync(
        TwitchUser user,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        if (user is null || string.IsNullOrWhiteSpace(message))
        {
            logger.LogWarning("TTS broadcast was skipped because the user or message is empty.");
            return;
        }

        try
        {
            await hubContext.Clients.Group(TtsConsumersGroupName).PlayTts(user, message);
            logger.LogInformation(
                "TTS broadcast was sent to hub consumers for user {User}",
                user.DisplayName
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast TTS message to hub consumers.");
        }
    }

    public async Task BroadcastStateAsync(
        TtsState state,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await hubContext.Clients.Group(TtsConsumersGroupName).UpdateTtsState(state);
            logger.LogInformation("TTS state update was sent to hub consumers: {@State}", state);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast TTS state to hub consumers.");
        }
    }
}