namespace MARS.Server.CustomLoggers.DatabaseLogger;

public static class DbLoggerExtensions
{
    public static ILoggingBuilder AddDbLogger(
        this ILoggingBuilder builder,
        Func<DbLoggerOptions> configure
    )
    {
        builder.Services.AddSingleton<ILoggerProvider>(new DbLoggerProvider(configure()));
        return builder;
    }
}
