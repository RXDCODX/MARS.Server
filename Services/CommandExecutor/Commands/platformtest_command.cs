using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class PlatformTestCommand(ILogger<PlatformTestCommand> logger) : BaseCommand
{
    public override string CommandName => "platformtest";
    public override string Description => "Тестовая команда для демонстрации работы с платформами";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.All];

    public override string[] Aliases => ["ptest"];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "Выполняется команда PlatformTest для платформы {Platform}",
            platform
        );

        return Task.FromResult(
            platform switch
            {
                Platform.Telegram => "🤖 **Тест платформы Telegram**\n\n"
                    + "Эта команда показывает, как работают разные ответы для разных платформ.\n"
                    + "Telegram поддерживает Markdown форматирование и имеет лимит в 4096 символов.\n\n"
                    + "📱 Платформа: Telegram\n"
                    + "📏 Максимальная длина: 4096 символов\n"
                    + "🎨 Поддержка: Markdown, эмодзи\n\n"
                    + "🕐 Время выполнения: "
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),

                Platform.Twitch => "🤖 Тест платформы Twitch\n\n"
                    + "Эта команда показывает, как работают разные ответы для разных платформ.\n"
                    + "Twitch имеет ограничения на длину сообщений в чате.\n\n"
                    + "📺 Платформа: Twitch\n"
                    + "📏 Максимальная длина: 500 символов\n"
                    + "🎨 Поддержка: Эмодзи, базовое форматирование\n\n"
                    + "🕐 Время: "
                    + DateTime.Now.ToString("HH:mm:ss"),

                Platform.Discord => "🤖 **Тест платформы Discord**\n\n"
                    + "Эта команда показывает, как работают разные ответы для разных платформ.\n"
                    + "Discord поддерживает Markdown и имеет большие лимиты.\n\n"
                    + "💬 Платформа: Discord\n"
                    + "📏 Максимальная длина: 2000 символов\n"
                    + "🎨 Поддержка: Markdown, эмодзи, вложения\n\n"
                    + "🕐 Время выполнения: "
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),

                _ => "🤖 Тест платформы\n\n"
                    + "Эта команда показывает, как работают разные ответы для разных платформ.\n"
                    + "Общая платформа используется по умолчанию.\n\n"
                    + "🌐 Платформа: Общая\n"
                    + "📏 Максимальная длина: 1000 символов\n"
                    + "🎨 Поддержка: Базовое форматирование\n\n"
                    + "🕐 Время: "
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            }
        );
    }
}
