using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MARS.Server.Hubs.Models.TunaHub;

public class DerivedColors
{
    [JsonProperty("average")]
    [JsonPropertyName("average")]
    public required string Average;

    [JsonProperty("waveText")]
    [JsonPropertyName("waveText")]
    public required string WaveText;

    [JsonProperty("miniPlayer")]
    [JsonPropertyName("miniPlayer")]
    public required string MiniPlayer;

    [JsonProperty("accent")]
    [JsonPropertyName("accent")]
    public required string Accent;
}
