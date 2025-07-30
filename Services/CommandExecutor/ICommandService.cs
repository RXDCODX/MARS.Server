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
    Task<string[]> GetUserCommandsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить названия админских команд
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив названий админских команд</returns>
    Task<string[]> GetAdminCommandsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить названия пользовательских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив названий пользовательских команд</returns>
    Task<string[]> GetUserCommandsAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить названия админских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив названий админских команд</returns>
    Task<string[]> GetAdminCommandsAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    );

    Task<CommandParameterInfo[]?> GetCommandParametersAsync(
        string commandName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить информацию о пользовательских командах
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации о пользовательских командах</returns>
    Task<CommandInfo[]> GetUserCommandsInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить информацию об админских командах
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации об админских командах</returns>
    Task<CommandInfo[]> GetAdminCommandsInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить информацию о пользовательских командах для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации о пользовательских командах</returns>
    Task<CommandInfo[]> GetUserCommandsInfoAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить информацию об админских командах для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации об админских командах</returns>
    Task<CommandInfo[]> GetAdminCommandsInfoAsync(
        Platform platforms,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Проверить, является ли команда админской
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если команда админская</returns>
    Task<bool> IsAdminCommandAsync(
        string commandName,
        CancellationToken cancellationToken = default
    );

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

public class CommandInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAdminCommand { get; set; }
    public CommandParameterInfo[] Parameters { get; set; } = [];
    public Platform[] AvailablePlatforms { get; set; } = [];
}
