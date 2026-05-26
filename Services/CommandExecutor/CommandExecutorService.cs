using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor;

public class CommandExecutorService(CommandFactory commandFactory)
    : BackgroundService,
        ICommandService
{
    private readonly Dictionary<string, BaseCommand> _commands = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    private void RegisterCommand(BaseCommand command)
    {
        _commands[command.CommandName] = command;

        // Добавляем алиасы из свойства Aliases
        foreach (var alias in command.Aliases)
        {
            _aliases[alias] = command.CommandName;
        }
    }

    public string[] GetUserCommands(CancellationToken cancellationToken = default)
    {
        string[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands
                    .Values.Where(c =>
                        !c.IsAdminCommand && c.IsVisibleIn(CommandVisibility.FullList)
                    )
                    .Select(c => $"/{c.CommandName} - {c.Description}"),
            ];
        }

        return result;
    }

    public string[] GetAdminCommands(CancellationToken cancellationToken = default)
    {
        string[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands
                    .Values.Where(c =>
                        c.IsAdminCommand && c.IsVisibleIn(CommandVisibility.FullList)
                    )
                    .Select(c => $"/{c.CommandName} - {c.Description}"),
            ];
        }

        return result;
    }

    public string[] GetUserCommands(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        string[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands
                    .Values.Where(c =>
                        !c.IsAdminCommand
                        && c.IsAvailableOnPlatform(platforms)
                        && c.IsVisibleIn(CommandVisibility.FullList)
                    )
                    .Select(c => $"/{c.CommandName} - {c.Description}"),
            ];
        }

        return result;
    }

    public string[] GetAdminCommands(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        string[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands
                    .Values.Where(c =>
                        c.IsAdminCommand
                        && c.IsAvailableOnPlatform(platforms)
                        && c.IsVisibleIn(CommandVisibility.FullList)
                    )
                    .Select(c => $"/{c.CommandName} - {c.Description}"),
            ];
        }

        return result;
    }

    public CommandParameterInfo[]? GetCommandParameters(
        string commandName,
        CancellationToken cancellationToken = default
    )
    {
        CommandParameterInfo[]? result = null;

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            // Проверяем алиасы
            if (_aliases.TryGetValue(commandName, out var actualCommandName))
            {
                commandName = actualCommandName;
            }

            if (_commands.TryGetValue(commandName, out var command))
            {
                result = command.GetParameterInfo();
            }
        }

        return result;
    }

    public BaseCommand[] GetUserCommandsInfo(CancellationToken cancellationToken = default)
    {
        BaseCommand[] result = [];

        if (_commands.Count > 0)
        {
            result = [.. _commands.Values.Where(c => !c.IsAdminCommand)];
        }

        return result;
    }

    public BaseCommand[] GetAdminCommandsInfo(CancellationToken cancellationToken = default)
    {
        BaseCommand[] result = [];

        if (_commands.Count > 0)
        {
            result = [.. _commands.Values.Where(c => c.IsAdminCommand)];
        }

        return result;
    }

    public BaseCommand[] GetUserCommandsInfo(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        BaseCommand[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands.Values.Where(c =>
                    !c.IsAdminCommand && c.IsAvailableOnPlatform(platforms)
                ),
            ];
        }

        return result;
    }

    public BaseCommand[] GetInlineCommandsInfo(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        BaseCommand[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands.Values.Where(c =>
                    !c.IsAdminCommand
                    && c.IsAvailableOnPlatform(platforms)
                    && c.IsVisibleIn(CommandVisibility.Inline)
                    && (c.SupportsInline || c.SupportsMediaInline)
                ),
            ];
        }

        return result;
    }

    public BaseCommand[] GetAdminCommandsInfo(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        BaseCommand[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands.Values.Where(c =>
                    c.IsAdminCommand && c.IsAvailableOnPlatform(platforms)
                ),
            ];
        }

        return result;
    }

    public bool IsAdminCommand(string commandName, CancellationToken cancellationToken = default)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            // Проверяем алиасы
            if (_aliases.TryGetValue(commandName, out var actualCommandName))
            {
                commandName = actualCommandName;
            }

            if (_commands.TryGetValue(commandName, out var command))
            {
                result = command.IsAdminCommand;
            }
        }

        return result;
    }

    public bool IsCommandAvailable(string commandName, Platform platform)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            // Проверяем алиасы
            if (_aliases.TryGetValue(commandName, out var actualCommandName))
            {
                commandName = actualCommandName;
            }

            if (_commands.TryGetValue(commandName, out var command))
            {
                result = command.IsAvailableOnPlatform(platform);
            }
        }

        return result;
    }

    public async Task<string> ExecuteCommandAsync(
        string commandName,
        string input,
        Platform platform,
        CancellationToken cancellationToken = default
    )
    {
        var result =
            $"Команда '{commandName}' не найдена. Используйте /commands или /c для списка доступных команд.";

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            // Проверяем алиасы
            if (_aliases.TryGetValue(commandName, out var actualCommandName))
            {
                commandName = actualCommandName;
            }

            if (_commands.TryGetValue(commandName, out var command))
            {
                // Проверяем доступность команды для платформы
                if (!command.IsAvailableOnPlatform(platform))
                {
                    result = $"Команда '{commandName}' недоступна на текущей платформе.";
                }
                else
                {
                    // Проверяем количество обязательных параметров
                    var commandInfo = command.GetParameterInfo();
                    var requiredParams = commandInfo.Where(p => p.Required).ToArray();
                    var inputParts = string.IsNullOrWhiteSpace(input)
                        ? []
                        : BaseCommand.ParseParametersWithQuotes(input);

                    if (inputParts.Length < requiredParams.Length)
                    {
                        var missingParam = requiredParams[inputParts.Length];
                        result =
                            $"Не хватает параметра '{missingParam.Name}'. Использование: {commandName} {string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"))}";
                    }
                    else
                    {
                        // Разбираем параметры из входной строки
                        var parameters = command.ParseParameters(input);

                        // Выполняем команду
                        result = await ExecuteCommandAsync(
                            commandName,
                            parameters,
                            platform,
                            cancellationToken
                        );
                    }
                }
            }
        }

        return result;
    }

    public async Task<string> ExecuteCommandAsync(
        string commandName,
        Dictionary<string, object> parameters,
        Platform platform,
        CancellationToken cancellationToken = default
    )
    {
        var result =
            $"Команда '{commandName}' не найдена. Используйте /commands или /c для списка доступных команд.";

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            // Проверяем алиасы
            if (_aliases.TryGetValue(commandName, out var actualCommandName))
            {
                commandName = actualCommandName;
            }

            if (_commands.TryGetValue(commandName, out var command))
            {
                // Проверяем доступность команды для платформы
                if (!command.IsAvailableOnPlatform(platform))
                {
                    result = $"Команда '{commandName}' недоступна на текущей платформе.";
                }
                else
                {
                    // Проверяем количество обязательных параметров
                    var commandInfo = command.GetParameterInfo();
                    var requiredParams = commandInfo.Where(p => p.Required).ToArray();

                    if (parameters.Count < requiredParams.Length)
                    {
                        var missingParam = requiredParams[parameters.Count];
                        result =
                            $"Не хватает параметра '{missingParam.Name}'. Использование: {commandName} {string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"))}";
                    }
                    else
                    {
                        // Выполняем команду
                        result = await command.ExecuteAsync(
                            parameters,
                            platform,
                            cancellationToken
                        );
                    }
                }
            }
        }

        return result;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Автоматически создаем все команды через фабрику
        var allCommands = commandFactory.CreateAllCommands();

        foreach (var command in allCommands)
        {
            RegisterCommand(command.Value);
        }

        return Task.FromResult(allCommands);
    }
}
