namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;

/// <summary>
/// Request body for synthesis endpoint
/// </summary>
public class FreeTtsSynthesisRequest
{
    public string Text { get; set; } = string.Empty;
    public string VoiceId { get; set; } = string.Empty;
    public string Ext { get; set; } = "mp3";
}
