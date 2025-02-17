namespace MARS.Server.Configuration;

public class TelegramConfiguration
{
    public static readonly string TelegramSection = "Telegram";
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public long[] AdminIdsArray { get; set; }
}
