using System.Text.Json.Serialization;

namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruAutoPostUpdateRequest
{
    public Guid Id { get; set; }

    public TargetPlatform TargetPlatform { get; set; }

    [JsonNumberHandling(
        JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    )]
    public ulong DiscordChannelId { get; set; }

    public long? TelegramChannelId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";

    public DateTime? ScheduledAtUtc { get; set; }
}
