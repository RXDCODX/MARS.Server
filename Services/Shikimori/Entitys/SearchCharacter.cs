namespace MARS.Server.Services.Shikimori.Entitys;
using System.Text.Json.Serialization;

public class SearchCharacter
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    [JsonPropertyName("russian")]
    public required string Russian { get; set; }
    [JsonPropertyName("image")]
    public required ShikiImage Image { get; set; }
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}
