using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Adapters;

/// <summary>
/// Адаптер для выполнения команд через API
/// </summary>
public class ApiCommandService : PlatformCommandServiceBase<string>
{
    private readonly CommandFactory _commandFactory;
    private readonly ILogger<ApiCommandService> _logger;
    private readonly Dictionary<string, BaseCommand> _commands;
    private readonly Dictionary<string, string> _aliases;

    public override Platform Platform => Platform.Api;

    protected override int DefaultMaxResponseLength => 10000; // API может поддерживать более длинные ответы

    public override char[] CommandPrefixes => ['/', '!'];

    public override IEnumerable<string> UserCommands =>
        _commands
            .Values.Where(c => !c.IsAdminCommand && c.IsAvailableOnPlatform(Platform.Api))
            .Select(c => $"/{c.CommandName} - {c.Description}");

    public override IEnumerable<string> AdminCommands =>
        _commands
            .Values.Where(c => c.IsAdminCommand && c.IsAvailableOnPlatform(Platform.Api))
            .Select(c => $"/{c.CommandName} - {c.Description}");

    public override Func<string, bool> IsAdmin => (userId) => true; // Для API все пользователи считаются администраторами

    public ApiCommandService(CommandFactory commandFactory, ILogger<ApiCommandService> logger)
    {
        _commandFactory = commandFactory;
        _logger = logger;
        _commands = new Dictionary<string, BaseCommand>(StringComparer.OrdinalIgnoreCase);
        _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        InitializeCommands();
    }

    private void InitializeCommands()
    {
        // Создаем все команды через фабрику
        var allCommands = _commandFactory.CreateAllCommands();

        foreach (var command in allCommands)
        {
            RegisterCommand(command.Value);
        }
    }

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

    /// <summary>
    /// Выполнить команду через API
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <param name="input">Входные параметры</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат выполнения команды</returns>
    public async Task<string> ExecuteCommandAsync(
        string commandName,
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var result =
            $"Команда '{commandName}' не найдена. Используйте /commands для списка доступных команд.";

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            try
            {
                // Обработка специальной команды commands
                if (commandName.Equals("commands", StringComparison.OrdinalIgnoreCase))
                {
                    var includeAdminCommands = IsUserAdmin(string.Empty);

                    result = GetCommandsList(
                        "api_user",
                        UserCommands,
                        AdminCommands,
                        includeAdminCommands
                    );
                }
                else
                {
                    // Проверяем алиасы
                    if (_aliases.TryGetValue(commandName, out var actualCommandName))
                    {
                        commandName = actualCommandName;
                    }

                    if (_commands.TryGetValue(commandName, out var command))
                    {
                        // Проверяем доступность команды для платформы API
                        if (!command.IsAvailableOnPlatform(Platform.Api))
                        {
                            result = $"Команда '{commandName}' недоступна на платформе API.";
                        }
                        else
                        {
                            // Проверяем количество обязательных параметров
                            var commandInfo = command.GetParameterInfo();
                            var requiredParams = commandInfo.Where(p => p.Required).ToArray();
                            var inputParts = string.IsNullOrWhiteSpace(input)
                                ? []
                                : input.Split(' ');

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
                                var commandResult = await command.ExecuteAsync(
                                    parameters,
                                    Platform.Api,
                                    cancellationToken
                                );

                                // Валидируем ответ для платформы
                                result = ValidateResponse(commandResult);

                                _logger.LogInformation(
                                    "Команда '{CommandName}' выполнена через API с результатом: {Result}",
                                    commandName,
                                    result.Length > 100
                                        ? string.Concat(result.AsSpan(0, 100), "...")
                                        : result
                                );
                            }
                        }
                    }
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Ошибка параметров для команды '{CommandName}'",
                    commandName
                );
                result = $"Ошибка параметров: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ошибка при выполнении команды '{CommandName}' через API",
                    commandName
                );
                result = $"Ошибка при выполнении команды '{commandName}': {ex.Message}";
            }
        }

        return result;
    }

    /// <summary>
    /// Получить информацию о команде
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>Информация о команде или null</returns>
    public CommandInfo? GetCommandInfo(string commandName)
    {
        CommandInfo? result = null;

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            // Проверяем алиасы
            if (_aliases.TryGetValue(commandName, out var actualCommandName))
            {
                commandName = actualCommandName;
            }

            if (_commands.TryGetValue(commandName, out var command))
            {
                result = new CommandInfo
                {
                    Name = command.CommandName,
                    Description = command.Description,
                    IsAdminCommand = command.IsAdminCommand,
                    Parameters = command.GetParameterInfo(),
                    AvailablePlatforms = command.GetAvailablePlatforms(),
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Получить список всех доступных команд для API
    /// </summary>
    /// <returns>Список команд</returns>
    public IEnumerable<CommandInfo> GetAvailableCommands()
    {
        return _commands
            .Values.Where(c => c.IsAvailableOnPlatform(Platform.Api))
            .Select(c => new CommandInfo
            {
                Name = c.CommandName,
                Description = c.Description,
                IsAdminCommand = c.IsAdminCommand,
                Parameters = c.GetParameterInfo(),
                AvailablePlatforms = c.GetAvailablePlatforms(),
            });
    }

    /// <summary>
    /// Проверить, является ли команда админской
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда админская</returns>
    public bool IsAdminCommand(string commandName)
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

    /// <summary>
    /// Проверить, доступна ли команда на платформе API
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда доступна</returns>
    public override bool IsCommandAvailable(string commandName)
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
                result = command.IsAvailableOnPlatform(Platform.Api);
            }
        }

        return result;
    }

    /// <summary>
    /// Валидировать ответ для платформы API
    /// </summary>
    /// <param name="response">Ответ команды</param>
    /// <returns>Валидный ответ</returns>
    public override string ValidateResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return response;
        }

        var maxLength = GetMaxResponseLength();

        if (response.Length <= maxLength)
        {
            return response;
        }

        // Для API используем более аккуратную обрезку
        var truncated = response.Substring(0, maxLength - 10);
        return truncated + "\n\n[Ответ обрезан...]";
    }

    /// <summary>
    /// Получить названия пользовательских команд
    /// </summary>
    /// <returns>Массив названий пользовательских команд</returns>
    public Task<string[]> GetUserCommandsAsync()
    {
        return Task.FromResult(UserCommands.ToArray());
    }

    /// <summary>
    /// Получить названия админских команд
    /// </summary>
    /// <returns>Массив названий админских команд</returns>
    public Task<string[]> GetAdminCommandsAsync()
    {
        return Task.FromResult(AdminCommands.ToArray());
    }

    /// <summary>
    /// Получить названия пользовательских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <returns>Массив названий пользовательских команд</returns>
    public Task<string[]> GetUserCommandsAsync(Platform platforms)
    {
        return Task.FromResult(
            _commands
                .Values.Where(c => !c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
                .Select(c => $"/{c.CommandName} - {c.Description}")
                .ToArray()
        );
    }

    /// <summary>
    /// Получить названия админских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <returns>Массив названий админских команд</returns>
    public Task<string[]> GetAdminCommandsAsync(Platform platforms)
    {
        return Task.FromResult(
            _commands
                .Values.Where(c => c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
                .Select(c => $"/{c.CommandName} - {c.Description}")
                .ToArray()
        );
    }

    /// <summary>
    /// Получить параметры команды
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>Массив параметров команды</returns>
    public Task<CommandParameterInfo[]> GetCommandParametersAsync(string commandName)
    {
        CommandParameterInfo[] result = [];

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

    /// <summary>
    /// Получить информацию о пользовательских командах
    /// </summary>
    /// <returns>Массив информации о пользовательских командах</returns>
    public Task<CommandInfo[]> GetUserCommandsInfoAsync()
    {
        return Task.FromResult(
            _commands
                .Values.Where(c => !c.IsAdminCommand && c.IsAvailableOnPlatform(Platform.Api))
                .Select(c => new CommandInfo
                {
                    Name = c.CommandName,
                    Description = c.Description,
                    IsAdminCommand = c.IsAdminCommand,
                    Parameters = c.GetParameterInfo(),
                    AvailablePlatforms = c.GetAvailablePlatforms(),
                    Visibility = c.Visibility,
                })
                .ToArray()
        );
    }

    /// <summary>
    /// Получить информацию об админских командах
    /// </summary>
    /// <returns>Массив информации об админских командах</returns>
    public Task<CommandInfo[]> GetAdminCommandsInfoAsync()
    {
        return Task.FromResult(
            _commands
                .Values.Where(c => c.IsAdminCommand && c.IsAvailableOnPlatform(Platform.Api))
                .Select(c => new CommandInfo
                {
                    Name = c.CommandName,
                    Description = c.Description,
                    IsAdminCommand = c.IsAdminCommand,
                    Parameters = c.GetParameterInfo(),
                    AvailablePlatforms = c.GetAvailablePlatforms(),
                    Visibility = c.Visibility,
                })
                .ToArray()
        );
    }

    /// <summary>
    /// Получить информацию о пользовательских командах для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <returns>Массив информации о пользовательских команд</returns>
    public Task<CommandInfo[]> GetUserCommandsInfoAsync(Platform platforms)
    {
        return Task.FromResult(
            _commands
                .Values.Where(c => !c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
                .Select(c => new CommandInfo
                {
                    Name = c.CommandName,
                    Description = c.Description,
                    IsAdminCommand = c.IsAdminCommand,
                    Parameters = c.GetParameterInfo(),
                    AvailablePlatforms = c.GetAvailablePlatforms(),
                    Visibility = c.Visibility,
                })
                .ToArray()
        );
    }

    /// <summary>
    /// Получить информацию об админских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <returns>Массив информации об админских команд</returns>
    public Task<CommandInfo[]> GetAdminCommandsInfoAsync(Platform platforms)
    {
        return Task.FromResult(
            _commands
                .Values.Where(c => c.IsAdminCommand && c.IsAvailableOnPlatform(platforms))
                .Select(c => new CommandInfo
                {
                    Name = c.CommandName,
                    Description = c.Description,
                    IsAdminCommand = c.IsAdminCommand,
                    Parameters = c.GetParameterInfo(),
                    AvailablePlatforms = c.GetAvailablePlatforms(),
                    Visibility = c.Visibility,
                })
                .ToArray()
        );
    }

    /// <summary>
    /// Проверить, является ли команда админской
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда админская</returns>
    public Task<bool> IsAdminCommandAsync(string commandName)
    {
        return Task.FromResult(IsAdminCommand(commandName));
    }
}
