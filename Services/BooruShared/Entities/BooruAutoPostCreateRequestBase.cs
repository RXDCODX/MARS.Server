namespace MARS.Server.Services.BooruShared.Entities;

public abstract class BooruAutoPostCreateRequestBase
{
    public string DiscordChannelId { get; set; } = "";

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";

    public string Message { get; set; } = "";
}
