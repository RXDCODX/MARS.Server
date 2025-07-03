#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MARS.Server.Services.Shikimori.Entitys;
using System.Text.Json.Serialization;

public class ShikiAnime
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("russian")]
    public string Russian { get; set; }
    [JsonPropertyName("image")]
    public ShikiImage Image { get; set; }
    [JsonPropertyName("url")]
    public string Url { get; set; }
    [JsonPropertyName("kind")]
    public string Kind { get; set; }
    [JsonPropertyName("score")]
    public string Score { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; }
    [JsonPropertyName("episodes")]
    public long Episodes { get; set; }
    [JsonPropertyName("episodes_aired")]
    public long EpisodesAired { get; set; }
    [JsonPropertyName("aired_on")]
    public object AiredOn { get; set; }
    [JsonPropertyName("released_on")]
    public object ReleasedOn { get; set; }
    [JsonPropertyName("roles")]
    public List<object> Roles { get; set; }
    [JsonPropertyName("role")]
    public string Role { get; set; }
}
