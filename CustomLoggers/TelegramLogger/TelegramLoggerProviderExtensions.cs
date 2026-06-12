using System;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace MARS.Server.CustomLoggers.TelegramLogger;

public static class TelegramLoggerProviderExtensions
{
    public static ILoggingBuilder AddTelegramLogger(
        this ILoggingBuilder loggerFactory,
        TelegramLoggerOptions options,
        Func<string, LogLevel, bool>? filter = null
    )
    {
        filter ??= (s, level) => true;

        var botClient = new TelegramBotClient(options.BotToken);
        loggerFactory.AddProvider(new TelegramLoggerProvider(botClient, options, filter));
        return loggerFactory;
    }

    public static ILoggingBuilder AddTelegramLogger(
        this ILoggingBuilder loggerFactory,
        Action<TelegramLoggerOptions> configure,
        Func<string, LogLevel, bool>? filter = null
    )
    {
        filter ??= (s, level) => true;

        var options = new TelegramLoggerOptions();
        configure(options);
        return loggerFactory.AddTelegramLogger(options, filter);
    }
}
