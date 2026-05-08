using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Adapters;

/// <summary>
/// Адаптер для выполнения команд через API
/// </summary>
public class ApiCommandService : PlatformCommandServiceBase<string>
{
    private readonly ICommandService _commandService;
    private readonly ILogger<ApiCommandService> _logger;

    public override Platform Platform => Platform.Api;

    protected override int DefaultMaxResponseLength => 10000; // API может поддерживать более длинные ответы

    public override char[] CommandPrefixes => ['/', '!'];

    public override IEnumerable<string> UserCommands =>
        _commandService.GetUserCommands(Platform.Api);

    public override IEnumerable<string> AdminCommands =>
        _commandService.GetAdminCommands(Platform.Api);

    public override Func<string, bool> IsAdmin => (userId) => true; // Для API все пользователи считаются администраторами

    public ApiCommandService(ICommandService commandService, ILogger<ApiCommandService> logger)
    {
        _commandService = commandService;
        _logger = logger;
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
                result = await _commandService.ExecuteCommandAsync(
                    commandName,
                    input,
                    Platform.Api,
                    cancellationToken
                );

                result = ValidateResponse(result);

                _logger.LogInformation(
                    "Команда '{CommandName}' выполнена через API с результатом: {Result}",
                    commandName,
                    result.Length > 100 ? string.Concat(result.AsSpan(0, 100), "...") : result
                );
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
    /// Проверить, является ли команда админской
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда админская</returns>
    public bool IsAdminCommand(string commandName)
    {
        return _commandService.IsAdminCommand(commandName);
    }

    /// <summary>
    /// Проверить, доступна ли команда на платформе API
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда доступна</returns>
    public override bool IsCommandAvailable(string commandName)
    {
        return _commandService.IsCommandAvailable(commandName, Platform.Api);
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
    /// Получить названия пользовательских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <returns>Массив названий пользовательских команд</returns>
    public string[] GetUserCommands(Platform platforms)
    {
        return _commandService.GetUserCommands(platforms);
    }

    /// <summary>
    /// Получить названия админских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <returns>Массив названий админских команд</returns>
    public string[] GetAdminCommands(Platform platforms)
    {
        return _commandService.GetAdminCommands(platforms);
    }

    /// <summary>
    /// Получить параметры команды
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>Массив параметров команды</returns>
    public CommandParameterInfo[]? GetCommandParameters(string commandName)
    {
        return _commandService.GetCommandParameters(commandName);
    }
}
