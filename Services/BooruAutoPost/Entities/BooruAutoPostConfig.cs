using System.ComponentModel.DataAnnotations;
using MARS.Server.Services.BooruShared.Entities;

namespace MARS.Server.Services.BooruAutoPost.Entities;

public class BooruAutoPostConfig
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public BooruSource Source { get; set; }

    public TargetPlatform TargetPlatform { get; set; } = TargetPlatform.Discord;

    public ulong DiscordChannelId { get; set; }

    public long? TelegramChannelId { get; set; }

    public int TargetPostCount { get; set; } = 1;

    public int? SpecificPostId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";

    public int PlanningHorizonDays { get; set; } = 60;

    public bool IsEnabled { get; set; } = true;

    public string Message { get; set; } = "";

    public TelegramParseMode TelegramParseMode { get; set; } = TelegramParseMode.Html;

    public DateTime? LastExecutedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
