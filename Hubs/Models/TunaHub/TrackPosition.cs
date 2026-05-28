namespace MARS.Server.Hubs.Models.TunaHub;

public class TrackPosition
{
    [JsonProperty("volume")]
    [JsonPropertyName("volume")]
    public int? Volume;

    [JsonProperty("index")]
    [JsonPropertyName("index")]
    public int? Index;
}
