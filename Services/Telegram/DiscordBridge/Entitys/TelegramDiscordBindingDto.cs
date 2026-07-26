using System;
using System.Text.Json.Serialization;

namespace MARS.Server.Services.Telegram.DiscordBridge.Entitys;

public class TelegramDiscordBindingDto
{
    public Guid Id { get; set; }
    public long TelegramChannelId { get; set; }

    [JsonNumberHandling(
        JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    )]
    public ulong DiscordChannelId { get; set; }

    public bool IsEnabled { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
