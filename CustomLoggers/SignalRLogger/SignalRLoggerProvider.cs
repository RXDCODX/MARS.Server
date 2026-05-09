using System.Collections.Concurrent;

namespace MARS.Server.CustomLoggers.SignalRLogger;

public class SignalRLoggerProvider(
    SignalRLoggerOptions options,
    Func<string, LogLevel, bool>? filter,
    LoggerHubRecursionGuard recursionGuard
) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, SignalRLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, CreateLoggerImplementation);
    }

    private SignalRLogger CreateLoggerImplementation(string categoryName)
    {
        return new SignalRLogger(categoryName, options, filter, recursionGuard);
    }

    public void Dispose()
    {
        _loggers.Clear();
        GC.SuppressFinalize(this);
    }
}
