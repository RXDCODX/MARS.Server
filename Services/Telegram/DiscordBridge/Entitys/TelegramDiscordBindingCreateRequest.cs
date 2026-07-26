using System.Text.Json.Serialization;

namespace MARS.Server.Services.Telegram.DiscordBridge.Entitys;

public class TelegramDiscordBindingCreateRequest
{
    public long TelegramChannelId { get; set; }

    [JsonNumberHandling(
        JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    )]
    public ulong DiscordChannelId { get; set; }
}
