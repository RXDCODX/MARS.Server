using System;
using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.Telegram.DiscordBridge.Entities;

/// <summary>
/// Состояние обработки Telegram канала для пересылки в Discord
/// </summary>
public class TelegramDiscordChannelState
{
    [Key]
    public long TelegramChannelId { get; set; }

    public int LastProcessedMessageId { get; set; }

    public DateTime LastUpdatedUtc { get; set; } = DateTime.Now;
}
