namespace MARS.Server.Hubs;

using MARS.Server.Hubs.Models.VoiceRecognition;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

/// <summary>
/// SignalR hub for voice recognition and speech-to-text functionality.
/// 
/// Receives voice messages from MARS.AudioController (acting as a client)
/// and broadcasts them to connected clients for live streaming purposes.
/// 
/// Optimized for low-latency message delivery in streaming scenarios.
/// </summary>
[SignalRHub("/hubs/voice-recognition", AutoDiscover.MethodsAndParams)]
public class VoiceRecognitionHub(ILogger<VoiceRecognitionHub> logger) : Hub<IVoiceRecognitionHub>
{
    private const string AudioControllerGroupName = "audio-controller";
    private const string ClientsGroupName = "clients";

    /// <summary>
    /// Receive voice message from AudioController.
    /// Called when speech has been recognized and should be broadcast to clients.
    /// </summary>
    /// <param name="message">The recognized voice message</param>
    public async Task ReceiveVoiceMessage(VoiceRecognitionMessageDto message)
    {
        if (message == null)
        {
            logger.LogWarning("Received null voice message from {ConnectionId}", Context.ConnectionId);
            return;
        }

        // Ensure timestamp is set
        if (string.IsNullOrWhiteSpace(message.Timestamp))
        {
            message.Timestamp = DateTime.UtcNow.ToString("O");
        }

        logger.LogInformation(
            "Voice message received from AudioController: text='{Text}', language={Language}, confidence={Confidence}",
            message.Text,
            message.Language,
            message.Confidence
        );

        // Broadcast to all connected clients except the sender
        await Clients.Others.VoiceMessageRecognized(message);

        // Also send acknowledgment to sender
        await Clients.Caller.VoiceMessageRecognized(message);
    }

    /// <summary>
    /// Receive voice activity detection event from AudioController.
    /// </summary>
    /// <param name="isActive">Whether voice activity is detected</param>
    public async Task VoiceActivityDetected(bool isActive)
    {
        var activity = new VoiceActivityDto { IsActive = isActive };

        logger.LogDebug(
            "Voice activity detected from AudioController: isActive={IsActive}",
            isActive
        );

        // Broadcast activity to all clients for real-time feedback
        await Clients.All.VoiceActivityUpdated(activity);
    }

    /// <summary>
    /// Called when AudioController joins as the voice source client.
    /// </summary>
    public async Task RegisterAsAudioSource()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AudioControllerGroupName);

        logger.LogInformation(
            "AudioController registered as voice source: {ConnectionId}",
            Context.ConnectionId
        );

        // Notify all clients that voice recognition is available
        await Clients.Group(ClientsGroupName).VoiceRecognitionStarted();
    }

    /// <summary>
    /// Called when a regular client joins to listen for voice messages.
    /// </summary>
    public async Task RegisterAsClient()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, ClientsGroupName);

        logger.LogInformation(
            "Client registered for voice messages: {ConnectionId}",
            Context.ConnectionId
        );
    }

    /// <summary>
    /// Called when a connection is established.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation(
            "Client connected to VoiceRecognitionHub: {ConnectionId}",
            Context.ConnectionId
        );

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a connection is closed.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation(
            "Client disconnected from VoiceRecognitionHub: {ConnectionId}",
            Context.ConnectionId
        );

        // Notify clients that voice recognition session might be affected
        await Clients.Group(ClientsGroupName).VoiceRecognitionStopped();

        await base.OnDisconnectedAsync(exception);
    }
}
