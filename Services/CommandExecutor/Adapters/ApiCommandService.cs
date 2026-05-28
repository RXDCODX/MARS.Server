using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.CommandExecutor.Adapters;

/// <summary>
/// Адаптер для выполнения команд через API
/// </summary>
public class ApiCommandService(ICommandService commandService, ILogger<ApiCommandService> logger)
    : PlatformCommandServiceBase<string>
{
    public override Platform Platform => Platform.Api;

    protected override int DefaultMaxResponseLength => 10000; // API может поддерживать более длинные ответы

    public override char[] CommandPrefixes => ['/', '!'];

    public override IEnumerable<string> UserCommands =>
        commandService.GetUserCommands(Platform.Api);

    public override IEnumerable<string> AdminCommands =>
        commandService.GetAdminCommands(Platform.Api);

    public override Func<string, bool> IsAdmin => _ => true; // Для API все пользователи считаются администраторами

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
                result = await commandService.ExecuteCommandAsync(
                    commandName,
                    input,
                    Platform.Api,
                    cancellationToken
                );

                result = ValidateResponse(result);

                logger.LogInformation(
                    "Команда '{CommandName}' выполнена через API с результатом: {Result}",
                    commandName,
                    result.Length > 100 ? string.Concat(result.AsSpan(0, 100), "...") : result
                );
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Ошибка параметров для команды '{CommandName}'", commandName);
                result = $"Ошибка параметров: {ex.Message}";
            }
            catch (Exception ex)
            {
                logger.LogError(
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
        return commandService.IsAdminCommand(commandName);
    }

    /// <summary>
    /// Проверить, доступна ли команда на платформе API
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда доступна</returns>
    public virtual bool IsCommandAvailable(string commandName)
    {
        return commandService.IsCommandAvailable(commandName, Platform.Api);
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
        return commandService.GetUserCommands(platforms);
    }

    /// <summary>
    /// Получить названия админских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <returns>Массив названий админских команд</returns>
    public string[] GetAdminCommands(Platform platforms)
    {
        return commandService.GetAdminCommands(platforms);
    }

    /// <summary>
    /// Получить параметры команды
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>Массив параметров команды</returns>
    public CommandParameterInfo[]? GetCommandParameters(string commandName)
    {
        return commandService.GetCommandParameters(commandName);
    }

    public BaseCommand[] GetUserCommandsInfo(Platform platform)
    {
        return commandService
            .GetUserCommandsInfo()
            .Where(e => e.AvailablePlatforms.Contains(Platform.Api))
            .ToArray();
    }

    public BaseCommand[] GetAdminCommandsInfo(Platform platform)
    {
        return commandService
            .GetAdminCommandsInfo()
            .Where(e => e.AvailablePlatforms.Contains(Platform.Api))
            .ToArray();
    }
}
