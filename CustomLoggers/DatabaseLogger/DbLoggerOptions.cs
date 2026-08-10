namespace MARS.Server.CustomLoggers.DatabaseLogger;

public class DbLoggerOptions
{
    public required LoggerDbContextFactory Factory { get; set; }
    public required LogLevel MinimumLogLevel { get; set; }
    public required IHostEnvironment Environment { get; set; }
}
