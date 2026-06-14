using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    string[] GetUserCommands(
        bool isAddDescription = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить названия админских команд
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив названий админских команд</returns>
    string[] GetAdminCommands(
        bool isAddDescription = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить названия пользовательских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <param name="b"></param>
    /// <returns>Массив названий пользовательских команд</returns>
    string[] GetUserCommands(
        Platform platforms,
        bool isAddDescription = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить названия админских команд для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <param name="b"></param>
    /// <returns>Массив названий админских команд</returns>
    string[] GetAdminCommands(
        Platform platforms,
        bool isAddDescription = true,
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
    BaseCommand[] GetUserCommandsInfo(
        bool isAddDescription = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить информацию об админских командах
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации об админских командах</returns>
    BaseCommand[] GetAdminCommandsInfo(
        bool isAddDescription = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить информацию о пользовательских командах для указанных платформ
    /// </summary>
    /// <param name="platforms">Платформы для фильтрации команд</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив информации о пользовательских командах</returns>
    BaseCommand[] GetUserCommandsInfo(
        Platform platforms,
        bool isAddDescription = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить информацию о командах, предназначенных для inline-выдачи
    /// </summary>
    BaseCommand[] GetInlineCommandsInfo(
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
        bool isAddDescription = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Проверить, является ли команда админской
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если команда админская</returns>
    bool IsAdminCommand(string commandName, CancellationToken cancellationToken = default);

    bool IsCommandAvailable(string commandName, Platform platform);

    Dictionary<string, object> ParseParameters(string input, CommandParameterInfo[]? commandInfo)
    {
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var inputParts = string.IsNullOrWhiteSpace(input)
            ? Array.Empty<string>()
            : BaseCommand.ParseParametersWithQuotes(input);

        // Заполняем именованные параметры по порядку, если они есть
        if (commandInfo is not null && commandInfo.Length > 0)
        {
            for (var i = 0; i < commandInfo.Length; i++)
            {
                var p = commandInfo[i];
                if (i < inputParts.Length)
                {
                    parameters[p.Name] = inputParts[i];
                }
            }
        }

        return parameters;
    }

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

    /// <summary>
    /// Выполнить команду
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <param name="parameters">Входные параметры</param>
    /// <param name="platform">Платформа</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат выполнения команды</returns>
    Task<string> ExecuteCommandAsync(
        string commandName,
        Dictionary<string, object> parameters,
        Platform platform,
        CancellationToken cancellationToken = default
    );
}
