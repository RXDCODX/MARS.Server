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

        // Добавляем специальные алиасы
        if (command.CommandName == "framedata")
        {
            _aliases["fd"] = command.CommandName;
        }
    }

    public Task<string[]> GetUserCommandsAsync(CancellationToken cancellationToken = default)
    {
        var commands = _commands
            .Values.Where(c => !c.IsAdminCommand)
            .Select(c => $"/{c.CommandName} - {c.Description}")
            .ToArray();

        return Task.FromResult(commands);
    }

    public Task<string[]> GetAdminCommandsAsync(CancellationToken cancellationToken = default)
    {
        var commands = _commands
            .Values.Where(c => c.IsAdminCommand)
            .Select(c => $"/{c.CommandName} - {c.Description}")
            .ToArray();

        return Task.FromResult(commands);
    }

    public Task<string[]> GetUserCommandsAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        var commands = _commands
            .Values.Where(c => !c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
            .Select(c => $"/{c.CommandName} - {c.Description}")
            .ToArray();

        return Task.FromResult(commands);
    }

    public Task<string[]> GetAdminCommandsAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        var commands = _commands
            .Values.Where(c => c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
            .Select(c => $"/{c.CommandName} - {c.Description}")
            .ToArray();

        return Task.FromResult(commands);
    }

    public Task<CommandParameterInfo[]?> GetCommandParametersAsync(
        string commandName,
        CancellationToken cancellationToken = default
    )
    {
        // Проверяем алиасы
        if (_aliases.TryGetValue(commandName, out var actualCommandName))
        {
            commandName = actualCommandName;
        }

        return !_commands.TryGetValue(commandName, out var command)
            ? Task.FromResult<CommandParameterInfo[]?>(null)
            : Task.FromResult<CommandParameterInfo[]?>(command.GetParameterInfo());
    }

    public Task<CommandInfo[]> GetUserCommandsInfoAsync(
        CancellationToken cancellationToken = default
    )
    {
        var commands = _commands
            .Values.Where(c => !c.IsAdminCommand)
            .Select(c => new CommandInfo
            {
                Name = c.CommandName,
                Description = c.Description,
                IsAdminCommand = c.IsAdminCommand,
                Parameters = c.GetParameterInfo(),
                AvailablePlatforms = c.GetAvailablePlatforms(),
            })
            .ToArray();

        return Task.FromResult(commands);
    }

    public Task<CommandInfo[]> GetAdminCommandsInfoAsync(
        CancellationToken cancellationToken = default
    )
    {
        var commands = _commands
            .Values.Where(c => c.IsAdminCommand)
            .Select(c => new CommandInfo
            {
                Name = c.CommandName,
                Description = c.Description,
                IsAdminCommand = c.IsAdminCommand,
                Parameters = c.GetParameterInfo(),
                AvailablePlatforms = c.GetAvailablePlatforms(),
            })
            .ToArray();

        return Task.FromResult(commands);
    }

    public Task<CommandInfo[]> GetUserCommandsInfoAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        var commands = _commands
            .Values.Where(c => !c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
            .Select(c => new CommandInfo
            {
                Name = c.CommandName,
                Description = c.Description,
                IsAdminCommand = c.IsAdminCommand,
                Parameters = c.GetParameterInfo(),
                AvailablePlatforms = c.GetAvailablePlatforms(),
            })
            .ToArray();

        return Task.FromResult(commands);
    }

    public Task<CommandInfo[]> GetAdminCommandsInfoAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    )
    {
        var commands = _commands
            .Values.Where(c => c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
            .Select(c => new CommandInfo
            {
                Name = c.CommandName,
                Description = c.Description,
                IsAdminCommand = c.IsAdminCommand,
                Parameters = c.GetParameterInfo(),
                AvailablePlatforms = c.GetAvailablePlatforms(),
            })
            .ToArray();

        return Task.FromResult(commands);
    }

    public Task<bool> IsAdminCommandAsync(
        string commandName,
        CancellationToken cancellationToken = default
    )
    {
        // Проверяем алиасы
        if (_aliases.TryGetValue(commandName, out var actualCommandName))
        {
            commandName = actualCommandName;
        }

        return !_commands.TryGetValue(commandName, out var command)
            ? Task.FromResult(false)
            : Task.FromResult(command.IsAdminCommand);
    }

    public Task<bool> IsCommandAvailableAsync(string commandName, Platform platform)
    {
        // Проверяем алиасы
        if (_aliases.TryGetValue(commandName, out var actualCommandName))
        {
            commandName = actualCommandName;
        }

        return !_commands.TryGetValue(commandName, out var command)
            ? Task.FromResult(false)
            : Task.FromResult(command.IsAvailableOnPlatform(platform));
    }

    public async Task<string> ExecuteCommandAsync(
        string commandName,
        string input,
        Platform platform,
        CancellationToken cancellationToken = default
    )
    {
        // Проверяем алиасы
        if (_aliases.TryGetValue(commandName, out var actualCommandName))
        {
            commandName = actualCommandName;
        }

        if (!_commands.TryGetValue(commandName, out var command))
        {
            return $"Команда '{commandName}' не найдена. Используйте /commands для списка доступных команд.";
        }

        // Проверяем количество обязательных параметров
        var commandInfo = command.GetParameterInfo();
        var requiredParams = commandInfo.Where(p => p.Required).ToArray();
        var inputParts = string.IsNullOrWhiteSpace(input) ? [] : input.Split(' ');

        if (inputParts.Length < requiredParams.Length)
        {
            var missingParam = requiredParams[inputParts.Length];
            return $"Не хватает параметра '{missingParam.Name}'. Использование: {commandName} {string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"))}";
        }

        // Разбираем параметры из входной строки
        var parameters = command.ParseParameters(input);

        // Выполняем команду
        var result = await command.ExecuteAsync(parameters, platform, cancellationToken);

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
