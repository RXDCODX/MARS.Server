namespace MARS.Server.Services.TelegramDiscordBridge.Entitys;

public class TelegramDiscordBindingCreateRequest
{
    public long TelegramChannelId { get; set; }
    public ulong DiscordChannelId { get; set; }
}
