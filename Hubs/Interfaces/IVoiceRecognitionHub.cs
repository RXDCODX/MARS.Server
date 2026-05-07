namespace MARS.Server.Hubs.Interfaces;

using MARS.Server.Hubs.Models.VoiceRecognition;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

/// <summary>
/// Interface for voice recognition hub client methods.
/// Defines messages that can be sent from server to clients.
/// </summary>
public interface IVoiceRecognitionHub
{
    /// <summary>
    /// Notify that voice recognition session has started.
    /// </summary>
    Task VoiceRecognitionStarted();

    /// <summary>
    /// Notify that voice recognition session has stopped.
    /// </summary>
    Task VoiceRecognitionStopped();

    /// <summary>
    /// Send voice activity detection status to clients.
    /// </summary>
    /// <param name="activity">Voice activity information</param>
    Task VoiceActivityUpdated(VoiceActivityDto activity);

    /// <summary>
    /// Broadcast recognized voice message to all connected clients.
    /// </summary>
    /// <param name="message">Recognized voice message</param>
    Task VoiceMessageRecognized(VoiceRecognitionMessageDto message);
}
