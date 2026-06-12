using System.Threading.Tasks;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Hubs.Interfaces;

using Models.VoiceRecognition;

/// <summary>
/// Interface for TTS hub client methods.
/// Defines messages that can be sent from the server to AudioController consumers.
/// </summary>
public interface IVoiceRecognitionHub
{
    /// <summary>
    /// Request a client to play a TTS message for a given Twitch user payload.
    /// </summary>
    /// <param name="user">Twitch user payload.</param>
    /// <param name="message">Text to speak.</param>
    Task PlayTts(TwitchUser user, string message);

    /// <summary>
    /// Send updated TTS state (stop flag / volume) to clients.
    /// </summary>
    /// <param name="state">TTS state payload.</param>
    Task UpdateTtsState(TtsState state);
}
