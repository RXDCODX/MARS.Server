namespace MARS.Server.Services.Telegram.DiscordBridge.Entitys;

public class TelegramDiscordBindingCreateRequest
{
    public long TelegramChannelId { get; set; }
    public ulong DiscordChannelId { get; set; }
}
