using System.Diagnostics.CodeAnalysis;

namespace MARS.Server.CustomLoggers.DatabaseLogger;

/// <summary>
/// Creates a new instance of <see cref="DbLogger" />.
/// </summary>
/// <param name="dbLoggerProvider">Instance of <see cref="DbLoggerProvider" />.</param>
public class DbLogger([NotNull] DbLoggerProvider dbLoggerProvider) : ILogger
{
    /// <summary>
    /// Instance of <see cref="DbLoggerProvider" />.
    /// </summary>
    private readonly DbLoggerProvider _dbLoggerProvider = dbLoggerProvider;
#pragma warning disable CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
#pragma warning disable CS8603 // Possible null reference return.

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }
#pragma warning restore CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
#pragma warning restore CS8603  // Possible null reference return.


    /// <summary>
    /// Whether to log the entry.
    /// </summary>
    /// <param name="logLevel"></param>
    /// <returns></returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    /// <summary>
    /// Used to log the entry.
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    /// <param name="logLevel">An instance of <see cref="LogLevel"/>.</param>
    /// <param name="eventId">The event's ID. An instance of <see cref="EventId"/>.</param>
    /// <param name="state">The event's state.</param>
    /// <param name="exception">The event's exception. An instance of <see cref="Exception" /></param>
    /// <param name="formatter">A delegate that formats </param>
    public async void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (
            !IsEnabled(logLevel)
            || logLevel < _dbLoggerProvider.Options.MinimumLogLevel
            || !_dbLoggerProvider.Options.Environment.IsProduction()
        )
        {
            return;
        }

        await Task.Factory.StartNew(async () =>
        {
            try
            {
                await using var dbContext = _dbLoggerProvider.Options.Factory.CreateDbContext();

                var log = new Log
                {
                    Message = formatter(state, exception),
                    StackTrace = exception?.StackTrace,
                    LogLevel = logLevel,
                };

                await dbContext.Logs.AddAsync(log);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Если не удалось записать в БД, выводим в консоль
                Console.WriteLine($"Failed to write log to database: {ex.Message}");
            }
        });
    }
}
