using System;
using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruAutoPostConfig
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public TargetPlatform TargetPlatform { get; set; } = TargetPlatform.Discord;

    public ulong DiscordChannelId { get; set; }

    public long? TelegramChannelId { get; set; }

    public Guid? BatchId { get; set; }

    public int? DanbooruPostId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";

    public DateTime? ScheduledAtUtc { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastExecutedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
