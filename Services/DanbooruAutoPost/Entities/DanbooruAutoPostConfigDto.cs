using System.Text.Json.Serialization;

namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruAutoPostConfigDto
{
    public Guid Id { get; set; }

    public TargetPlatform TargetPlatform { get; set; }

    [JsonNumberHandling(
        JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    )]
    public ulong DiscordChannelId { get; set; }

    public long? TelegramChannelId { get; set; }

    public int TargetPostCount { get; set; }

    public int? DanbooruPostId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";

    public DateTime? ScheduledAtUtc { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime? LastExecutedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
