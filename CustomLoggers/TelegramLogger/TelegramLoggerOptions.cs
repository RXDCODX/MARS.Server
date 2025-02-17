namespace MARS.Server.CustomLoggers.TelegramLogger;

public class TelegramLoggerOptions
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public string BotToken { get; set; }

    /// <summary>
    ///     Unique identifier for the target chat or username of the target channel (in the format @channelusername)
    /// </summary>
    public long[] ChatId { get; set; }

    /// <summary>
    ///     The name of the source of logs
    /// </summary>
    public string SourceName { get; set; }

    public LogLevel MinimumLevel { get; set; } = LogLevel.None;
}
