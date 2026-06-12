using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MARS.Server.CustomLoggers.SignalRLogger;

public static class SignalRLoggerExtensions
{
    /// <summary>
    /// Добавляет SignalR логгер в систему логирования
    /// </summary>
    /// <param name="builder">Построитель логирования</param>
    /// <param name="configure">Конфигурация опций логгера</param>
    /// <returns>Построитель логирования</returns>
    public static ILoggingBuilder AddSignalRLogger(
        this ILoggingBuilder builder,
        Action<SignalRLoggerOptions>? configure = null
    )
    {
        var options = new SignalRLoggerOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton<LoggerHubRecursionGuard>();

        builder.Services.AddSingleton<ILoggerProvider>(serviceProvider => new SignalRLoggerProvider(
            options,
            null,
            serviceProvider.GetRequiredService<LoggerHubRecursionGuard>()
        ));

        return builder;
    }

    /// <summary>
    /// Добавляет SignalR логгер с фильтром
    /// </summary>
    /// <param name="builder">Построитель логирования</param>
    /// <param name="filter">Фильтр для категорий и уровней логирования</param>
    /// <param name="configure">Конфигурация опций логгера</param>
    /// <returns>Построитель логирования</returns>
    public static ILoggingBuilder AddSignalRLogger(
        this ILoggingBuilder builder,
        Func<string, LogLevel, bool> filter,
        Action<SignalRLoggerOptions>? configure = null
    )
    {
        var options = new SignalRLoggerOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton<LoggerHubRecursionGuard>();

        builder.Services.AddSingleton<ILoggerProvider>(serviceProvider => new SignalRLoggerProvider(
            options,
            filter,
            serviceProvider.GetRequiredService<LoggerHubRecursionGuard>()
        ));

        return builder;
    }
}
