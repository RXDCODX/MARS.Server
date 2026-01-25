namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;

/// <summary>
/// Response from history endpoint
/// </summary>
public class FreeTtsHistoryResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<FreeTtsHistoryItem> Data { get; set; } = new();
}

/// <summary>
/// Historical synthesis record
/// </summary>
public class FreeTtsHistoryItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Local model for tracking processed synthesis
/// </summary>
public class ProcessedSynthesis
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string VoiceId { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public bool SentToPlayback { get; set; }
}
