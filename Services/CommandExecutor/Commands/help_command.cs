namespace MARS.Server.Services.CommandExecutor.Commands;

public class HelpCommand(ICommandService commandService) : BaseCommand
{
    public override string CommandName => "help";
    public override string Description =>
        "Показывает справку по возможностям бота и форматам медиа, или информацию о конкретной команде";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override CommandVisibility Visibility => CommandVisibility.All;

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "commandName",
                Description = "Название команды для получения справки",
                Type = "string",
                Required = false,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        var hasCommandName =
            parameters.TryGetValue("commandName", out var commandNameObj)
            && !string.IsNullOrWhiteSpace(commandNameObj?.ToString());

        if (hasCommandName)
        {
            var commandName = commandNameObj!.ToString()!.Trim();

            var prefixes = GetCommandPrefixesForPlatform(platform);
            commandName = commandName.TrimStart(prefixes);

            var commandHelp = await GetCommandHelp(commandName, platform, cancellationToken);

            result = !string.IsNullOrWhiteSpace(commandHelp)
                ? commandHelp
                : $"Команда '{commandName}' не найдена. Используйте /commands или /c для списка доступных команд.";
        }
        else
        {
            result = "Параметр с названием команды пуст";
        }

        return result;
    }

    private static char[] GetCommandPrefixesForPlatform(Platform platform)
    {
        char[] result = platform switch
        {
            Platform.Twitch => ['!'],
            Platform.Telegram => ['/'],
            _ => ['/', '!'],
        };

        return result;
    }

    private Task<string> GetCommandHelp(
        string commandName,
        Platform platform,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = string.Empty;

            if (!string.IsNullOrWhiteSpace(commandName))
            {
                var userCommands = commandService.GetUserCommandsInfo(platform, cancellationToken);
                var adminCommands = commandService.GetAdminCommandsInfo(
                    platform,
                    cancellationToken
                );

                var allCommands = userCommands.Concat(adminCommands).ToArray();

                var command = allCommands.FirstOrDefault(c =>
                    c.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase)
                );

                if (command != null)
                {
                    result = FormatCommandHelp(command, platform);
                }
            }

            return Task.FromResult(result);
        }
        catch (Exception exception)
        {
            return Task.FromException<string>(exception);
        }
    }

    private static string FormatCommandHelp(BaseCommand command, Platform platform)
    {
        var result = string.Empty;

        if (command != null)
        {
            var prefix = platform switch
            {
                Platform.Twitch => "!",
                Platform.Telegram => "/",
                Platform.Api => "/",
                _ => "/",
            };

            var commandType = command.IsAdminCommand
                ? "Админская команда"
                : "Пользовательская команда";

            var parametersInfo = string.Empty;
            if (command.Parameters.Length > 0)
            {
                var paramsList = command.Parameters.Select(p =>
                {
                    var required = p.Required ? "(обязательный)" : "(опциональный)";
                    var defaultValue = !string.IsNullOrWhiteSpace(p.DefaultValue)
                        ? $", по умолчанию: {p.DefaultValue}"
                        : "";
                    return $"  • {p.Name} ({p.Type}) {required}{defaultValue}\n    {p.Description}";
                });
                parametersInfo = $"\n\n📋 Параметры:\n{string.Join("\n", paramsList)}";
            }
            else
            {
                parametersInfo = "\n\n📋 Параметры: нет";
            }

            var usage =
                command.Parameters.Length > 0
                    ? $"\n\n💡 Использование:\n{prefix}{command.CommandName} {string.Join(" ", command.Parameters.Select(p => p.Required ? $"<{p.Name}>" : $"[{p.Name}]"))}"
                    : $"\n\n💡 Использование:\n{prefix}{command.CommandName}";

            var platforms = string.Join(", ", command.AvailablePlatforms.Select(p => p.ToString()));

            result = $"""
                {commandType}: {prefix}{command.CommandName}

                📝 Описание:
                {command.Description}
                {parametersInfo}
                {usage}

                🌐 Доступна на платформах: {platforms}
                """;
        }

        return result;
    }
}
