namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;

/// <summary>
/// Response from synthesis endpoint
/// </summary>
public class FreeTtsSynthesisResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Data { get; set; }
}
