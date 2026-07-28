using System.Text.Json.Serialization;

namespace MARS.Server.Services.NSFWBooru.Entities;

public class NSFWBooruAutoPostCreateRequest
{
    [JsonNumberHandling(
        JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    )]
    public ulong DiscordChannelId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";
}
