namespace MARS.Server.Hubs.Models.TunaHub;

public class Cover
{
    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public required string Type;

    [JsonProperty("uri")]
    [JsonPropertyName("uri")]
    public required string Uri;

    [JsonProperty("prefix")]
    [JsonPropertyName("prefix")]
    public required string Prefix;
}
