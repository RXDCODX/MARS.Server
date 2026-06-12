using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MARS.Server.Hubs.Models.TunaHub;

public class LyricsInfo
{
    [JsonProperty("hasAvailableSyncLyrics")]
    [JsonPropertyName("hasAvailableSyncLyrics")]
    public bool? HasAvailableSyncLyrics;

    [JsonProperty("hasAvailableTextLyrics")]
    [JsonPropertyName("hasAvailableTextLyrics")]
    public bool? HasAvailableTextLyrics;
}
