using System.Collections.Concurrent;

namespace MARS.Server.CustomLoggers.TelegramLogger;

public class TelegramLoggerProvider : ILoggerProvider
{
    private readonly Func<string, LogLevel, bool>? _filter;

    private readonly ConcurrentDictionary<string, TelegramLogger> _loggers = new();

    private readonly TelegramLoggerSender _messageQueue;
    private readonly TelegramLoggerOptions _options;

    public TelegramLoggerProvider(
        ITelegramBotClient botClient,
        TelegramLoggerOptions options,
        Func<string, LogLevel, bool>? filter
    )
    {
        if (options.ChatId.Length == 0)
            throw new ArgumentException(
                "Log receiver Id should not be null, empty or be a whitespace"
            );

        if (
            string.IsNullOrEmpty(options.SourceName)
            || string.IsNullOrWhiteSpace(options.SourceName)
        )
            throw new ArgumentException("Source name should not be null, empty or be a whitespace");

        _filter = filter;
        _options = options;
        _messageQueue = new TelegramLoggerSender(botClient, options.ChatId);
    }

    public void Dispose()
    {
        _messageQueue.Dispose();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, CreateLoggerImplementation);
    }

    private TelegramLogger CreateLoggerImplementation(string categoryName)
    {
        return new TelegramLogger(categoryName, _messageQueue, _options, _filter);
    }
}
