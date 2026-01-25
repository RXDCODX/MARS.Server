namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;

/// <summary>
/// Represents a TTS voice from FreeTTS API
/// </summary>
public class FreeTtsVoice
{
    public string Id { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sex { get; set; } = string.Empty;
}
