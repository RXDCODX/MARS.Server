using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor;

/// <summary>
/// Интерфейс для сервиса команд
/// </summary>
public interface ICommandService
{
    /// <summary>
    /// Получить названия пользовательских команд
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив названий пользовательских команд</returns>
    string[] GetUserCommands(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить названия админских команд
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив названий админских команд</returns>
    string[] GetAdminCommands(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить названия пользовательских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив названий пользовательских команд</returns>
    string[] GetUserCommands(
        Platform platforms,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить названия админских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив названий админских команд</returns>
    string[] GetAdminCommands(
        Platform platforms,
        CancellationToken cancellationToken = default
    );

    CommandParameterInfo[]? GetCommandParameters(
        string commandName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить информацию о пользовательских командах
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации о пользовательских командах</returns>
    BaseCommand[] GetUserCommandsInfo(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить информацию об админских командах
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации об админских командах</returns>
    BaseCommand[] GetAdminCommandsInfo(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить информацию о пользовательских командах для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации о пользовательских командах</returns>
    BaseCommand[] GetUserCommandsInfo(
        Platform platforms,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить информацию об админских командах для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации об админских командах</returns>
    BaseCommand[] GetAdminCommandsInfo(
        Platform platforms,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Проверить, является ли команда админской
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если команда админская</returns>
    bool IsAdminCommand(
        string commandName,
        CancellationToken cancellationToken = default
    );

    bool IsCommandAvailable(string commandName, Platform platform);

    /// <summary>
    /// Выполнить команду
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <param name="input">Входные параметры</param>
    /// <param name="platform">Платформа</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат выполнения команды</returns>
    Task<string> ExecuteCommandAsync(
        string commandName,
        string input,
        Platform platform,
        CancellationToken cancellationToken = default
    );
}
