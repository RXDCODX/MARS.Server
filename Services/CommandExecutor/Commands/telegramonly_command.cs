using System.Collections.Generic;
using System.Threading;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TelegramonlyCommand : BaseCommand
{
    public override string CommandName => "telegramonly";
    public override string Description => "Команда, доступная только в Telegram";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "message",
                Description = "Сообщение для отправки",
                Type = "string",
                Required = true,
            },
        ];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var message = parameters["message"].ToString() ?? "Пустое сообщение";

        var result = $"""
            ✅ Команда выполнена в Telegram!
            📝 Отправленное сообщение: {message}

            Эта команда доступна только в Telegram из-за специфики платформы.
            """;

        return Task.FromResult(result);
    }
}
