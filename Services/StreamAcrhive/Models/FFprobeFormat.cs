using System.Text.Json.Serialization;

namespace MARS.Server.Services.StreamAcrhive.Models;

public class FFprobeFormat
{
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; set; }
}
