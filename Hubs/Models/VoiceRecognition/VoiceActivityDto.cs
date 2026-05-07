namespace MARS.Server.Hubs.Models.VoiceRecognition;

/// <summary>
/// Parameter object for voice activity detection events.
/// </summary>
public class VoiceActivityDto
{
    /// <summary>
    /// Whether voice activity is currently detected.
    /// </summary>
    public bool IsActive { get; set; }
}
