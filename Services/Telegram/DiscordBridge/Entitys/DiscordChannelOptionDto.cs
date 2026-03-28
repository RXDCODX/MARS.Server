namespace MARS.Server.Services.Telegram.DiscordBridge.Entitys;

public class DiscordChannelOptionDto
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ulong GuildId { get; set; }
    public string GuildName { get; set; } = string.Empty;
}
