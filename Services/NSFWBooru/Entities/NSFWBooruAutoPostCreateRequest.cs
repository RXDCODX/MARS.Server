namespace MARS.Server.Services.NSFWBooru.Entities;

public class NSFWBooruAutoPostCreateRequest
{
    public ulong DiscordChannelId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";
}
