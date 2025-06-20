namespace MARS.Server.CustomLoggers.DatabaseLogger;

[ProviderAlias("Database")]
public class DbLoggerProvider(DbLoggerOptions options) : ILoggerProvider
{
    public readonly DbLoggerOptions Options = options; // Stores all the options.

    /// <summary>
    /// Creates a new instance of the db logger.
    /// </summary>
    /// <param name="categoryName"></param>
    /// <returns></returns>
    public ILogger CreateLogger(string categoryName)
    {
        return new DbLogger(this);
    }

    public void Dispose() { }
}
