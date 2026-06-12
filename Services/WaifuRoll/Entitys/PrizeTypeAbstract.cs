using System.Text.Json.Serialization;

namespace MARS.Server.Services.WaifuRoll.Entitys;

public abstract class PrizeTypeAbstract
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("image")]
    public required string Image { get; set; }

    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

public class PrizeType : PrizeTypeAbstract;
