using System.Text.Json.Serialization;

namespace MARS.Server.Services.Telegram.DiscordBridge.Entitys;

public class DiscordChannelOptionDto
{
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public ulong Id { get; set; }

    public string Name { get; set; } = string.Empty;

    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public ulong GuildId { get; set; }

    public string GuildName { get; set; } = string.Empty;
}
