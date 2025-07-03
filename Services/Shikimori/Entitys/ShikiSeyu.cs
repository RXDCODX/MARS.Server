#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MARS.Server.Services.Shikimori.Entitys;
using System.Text.Json.Serialization;

public class ShikiSeyu
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("russian")]
    public string? Russian { get; set; }
    [JsonPropertyName("image")]
    public ShikiImage Image { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
