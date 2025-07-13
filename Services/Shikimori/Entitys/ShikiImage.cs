#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MARS.Server.Services.Shikimori.Entitys;
using System.Text.Json.Serialization;

public class ShikiImage
{
    [JsonPropertyName("original")]
    public string Original { get; set; }
    [JsonPropertyName("preview")]
    public string? Preview { get; set; }
    [JsonPropertyName("x96")]
    public string? X96 { get; set; }
    [JsonPropertyName("x48")]
    public string? X48 { get; set; }
}
