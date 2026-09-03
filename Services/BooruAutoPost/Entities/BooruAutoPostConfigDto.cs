using MARS.Server.Services.BooruShared.Entities;

namespace MARS.Server.Services.BooruAutoPost.Entities;

public class BooruAutoPostConfigDto
{
    public Guid Id { get; set; }

    public BooruSource Source { get; set; }

    public TargetPlatform TargetPlatform { get; set; }

    public string DiscordChannelId { get; set; } = "";

    public string? TelegramChannelId { get; set; }

    public int TargetPostCount { get; set; }

    public int? SpecificPostId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";

    public int PlanningHorizonDays { get; set; }

    public int PendingPostsCount { get; set; }

    public DateTime? NextScheduledAtUtc { get; set; }

    public string Message { get; set; } = "";

    public TelegramParseMode TelegramParseMode { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime? LastExecutedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
