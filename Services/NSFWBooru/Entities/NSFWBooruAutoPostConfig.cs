using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.NSFWBooru.Entities;

public class NSFWBooruAutoPostConfig
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public ulong DiscordChannelId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastExecutedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
