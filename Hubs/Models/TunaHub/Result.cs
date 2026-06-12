using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MARS.Server.Hubs.Models.TunaHub;

public class Result
{
    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public required string Type;

    [JsonProperty("track")]
    [JsonPropertyName("track")]
    public required Track Track;
}
