namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruAutoPostCreateRequest
{
    public ulong DiscordChannelId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";
}
