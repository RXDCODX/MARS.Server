using System.Collections.Generic;

namespace MARS.Server.Services.StreamAcrhive_UNUSED.Models;

public class FFprobeOutput
{
    [JsonPropertyName("format")]
    public FFprobeFormat? Format { get; set; }

    [JsonPropertyName("streams")]
    public List<FFprobeStream>? Streams { get; set; }
}
