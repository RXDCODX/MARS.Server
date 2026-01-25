namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;

/// <summary>
/// Response from FreeTTS list endpoint
/// </summary>
public class FreeTtsListResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public FreeTtsListData Data { get; set; } = new();
}

/// <summary>
/// Data container in list response
/// </summary>
public class FreeTtsListData
{
    public List<FreeTtsVoice> Voices { get; set; } = new();
    public List<FreeTtsLanguage> Langs { get; set; } = new();
}
