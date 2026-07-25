namespace MARS.Server.Hubs.Models.VoiceRecognition;

/// <summary>
/// Represents TTS playback state that server can broadcast to consumers.
/// </summary>
public class TtsState
{
    /// <summary>
    /// If true, clients should stop current playback.
    /// </summary>
    public bool IsStopped { get; set; }

    /// <summary>
    /// Playback volume in range 0.0 .. 1.0 (or provider-specific scale).
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// If true, AudioController should send generated audio to the hub for Discord relay
    /// instead of playing it locally.
    /// </summary>
    public bool RelayToDiscord { get; set; }
}
