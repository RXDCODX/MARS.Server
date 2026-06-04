namespace MARS.Server.Hubs.Models.TunaHub;

public class Artist
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public int? Id;

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public required string Name;

    [JsonProperty("various")]
    [JsonPropertyName("various")]
    public bool? Various;

    [JsonProperty("composer")]
    [JsonPropertyName("composer")]
    public bool? Composer;

    [JsonProperty("available")]
    [JsonPropertyName("available")]
    public bool? Available;

    [JsonProperty("cover")]
    [JsonPropertyName("cover")]
    public required Cover Cover;

    [JsonProperty("genres")]
    [JsonPropertyName("genres")]
    public required List<object> Genres;

    [JsonProperty("disclaimers")]
    [JsonPropertyName("disclaimers")]
    public required List<object> Disclaimers;
}
