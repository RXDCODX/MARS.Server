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

    public Task<string[]> GetUserCommandsAsync(CancellationToken cancellationToken = default)
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

        return Task.FromResult(result);
    }

    public Task<string[]> GetAdminCommandsAsync(CancellationToken cancellationToken = default)
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

        return Task.FromResult(result);
    }

    public Task<string[]> GetUserCommandsAsync(
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

        return Task.FromResult(result);
    }

    public Task<string[]> GetAdminCommandsAsync(
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

        return Task.FromResult(result);
    }

    public Task<CommandParameterInfo[]?> GetCommandParametersAsync(
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

        return Task.FromResult(result);
    }

    public Task<CommandInfo[]> GetUserCommandsInfoAsync(
        CancellationToken cancellationToken = default
    )
    {
        CommandInfo[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands
                    .Values.Where(c => !c.IsAdminCommand)
                    .Select(c => new CommandInfo
                    {
                        Name = c.CommandName,
                        Description = c.Description,
                        IsAdminCommand = c.IsAdminCommand,
                        Parameters = c.GetParameterInfo(),
                        AvailablePlatforms = c.GetAvailablePlatforms(),
                        Visibility = c.Visibility,
                    }),
            ];
        }

        return Task.FromResult(result);
    }

    public Task<CommandInfo[]> GetAdminCommandsInfoAsync(
        CancellationToken cancellationToken = default
    )
    {
        CommandInfo[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands
                    .Values.Where(c => c.IsAdminCommand)
                    .Select(c => new CommandInfo
                    {
                        Name = c.CommandName,
                        Description = c.Description,
                        IsAdminCommand = c.IsAdminCommand,
                        Parameters = c.GetParameterInfo(),
                        AvailablePlatforms = c.GetAvailablePlatforms(),
                        Visibility = c.Visibility,
                    }),
            ];
        }

        return Task.FromResult(result);
    }

    public Task<CommandInfo[]> GetUserCommandsInfoAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        CommandInfo[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands
                    .Values.Where(c => !c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
                    .Select(c => new CommandInfo
                    {
                        Name = c.CommandName,
                        Description = c.Description,
                        IsAdminCommand = c.IsAdminCommand,
                        Parameters = c.GetParameterInfo(),
                        AvailablePlatforms = c.GetAvailablePlatforms(),
                        Visibility = c.Visibility,
                    }),
            ];
        }

        return Task.FromResult(result);
    }

    public Task<CommandInfo[]> GetAdminCommandsInfoAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        CommandInfo[] result = [];

        if (_commands.Count > 0)
        {
            result =
            [
                .. _commands
                    .Values.Where(c => c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
                    .Select(c => new CommandInfo
                    {
                        Name = c.CommandName,
                        Description = c.Description,
                        IsAdminCommand = c.IsAdminCommand,
                        Parameters = c.GetParameterInfo(),
                        AvailablePlatforms = c.GetAvailablePlatforms(),
                        Visibility = c.Visibility,
                    }),
            ];
        }

        return Task.FromResult(result);
    }

    public Task<bool> IsAdminCommandAsync(
        string commandName,
        CancellationToken cancellationToken = default
    )
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

        return Task.FromResult(result);
    }

    public Task<bool> IsCommandAvailableAsync(string commandName, Platform platform)
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

        return Task.FromResult(result);
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
                    var inputParts = string.IsNullOrWhiteSpace(input) ? [] : input.Split(' ');

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
                        result = await command.ExecuteAsync(parameters, platform, cancellationToken);
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
