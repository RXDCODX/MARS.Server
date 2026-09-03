namespace MARS.Server.Configuration;

public class TelegramProxyConfiguration
{
    public static readonly string Configuration = "Proxy";

    public string? BotProxyUrl { get; set; }

    public string? BotProxyType { get; set; }

    public string? WTelegramProxyUrl { get; set; }

    public string? WTelegramProxyType { get; set; }
}
