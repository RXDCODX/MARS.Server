using System.Text.Json.Serialization;

namespace MARS.Server.Services.Twitch.Entitys;

public class MikuModulePrizeType
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("image")]
    public required string Image { get; set; }

    [JsonPropertyName("text")]
    public required string Text { get; set; }
}
