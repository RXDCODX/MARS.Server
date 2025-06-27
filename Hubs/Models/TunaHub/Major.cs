using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MARS.Server.Hubs.Models.TunaHub;

public class Major
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public int? Id;

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public required string Name;
}
