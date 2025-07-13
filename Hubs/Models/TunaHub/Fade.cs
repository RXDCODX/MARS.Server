using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MARS.Server.Hubs.Models.TunaHub;

public class Fade
{
    [JsonProperty("inStart")]
    [JsonPropertyName("inStart")]
    public double? InStart;

    [JsonProperty("inStop")]
    [JsonPropertyName("inStop")]
    public double? InStop;

    [JsonProperty("outStart")]
    [JsonPropertyName("outStart")]
    public double? OutStart;

    [JsonProperty("outStop")]
    [JsonPropertyName("outStop")]
    public double? OutStop;
}
