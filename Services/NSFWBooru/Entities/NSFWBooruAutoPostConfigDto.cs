namespace MARS.Server.Services.NSFWBooru.Entities;

public class NSFWBooruAutoPostConfigDto
{
    public Guid Id { get; set; }

    public string DiscordChannelId { get; set; } = "";

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";

    public bool IsEnabled { get; set; }

    public string Message { get; set; } = "";

    public DateTime? LastExecutedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
