using System.Text.Json.Serialization;

namespace MARS.Server.Services.StreamAcrhive_UNUSED.Models;

public class FFprobeStream
{
    [JsonPropertyName("codec_type")]
    public string? CodecType { get; set; }

    [JsonPropertyName("codec_name")]
    public string? CodecName { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("r_frame_rate")]
    public string? RFrameRate { get; set; }
}
