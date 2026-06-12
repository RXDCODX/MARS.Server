using System;

namespace MARS.Server.Services.Telegram.DiscordBridge.Entitys;

public class TelegramDiscordChannelStateDto
{
    public long TelegramChannelId { get; set; }
    public int LastProcessedMessageId { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
