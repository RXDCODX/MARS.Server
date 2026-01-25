namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;

/// <summary>
/// Health check response
/// </summary>
public class FreeTtsHealthResponse
{
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
}
