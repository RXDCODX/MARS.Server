using System.Text.Json.Serialization;

namespace MARS.Server.Services.StreamAcrhive.Models;

public class FFprobeOutput
{
    [JsonPropertyName("format")]
    public FFprobeFormat? Format { get; set; }

    [JsonPropertyName("streams")]
    public List<FFprobeStream>? Streams { get; set; }
}
