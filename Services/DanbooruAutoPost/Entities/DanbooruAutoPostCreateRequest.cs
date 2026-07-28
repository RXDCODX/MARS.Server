using System.Text.Json.Serialization;

namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruAutoPostCreateRequest
{
    [JsonNumberHandling(
        JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    )]
    public ulong DiscordChannelId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";
}
