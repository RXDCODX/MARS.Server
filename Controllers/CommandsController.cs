using MARS.Server.Services.CommandExecutor;
using MARS.Server.Services.CommandExecutor.Adapters;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для работы с командами
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CommandsController(
    ApiCommandService commandService,
    ILogger<CommandsController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить пользовательские команды
    /// </summary>
    /// <returns>Список пользовательских команд</returns>
    [HttpGet("user")]
    public ActionResult<string> GetUserCommands()
    {
        try
        {
            var commands = commandService.GetCommandsList("api_user", commandService.UserCommands, commandService.AdminCommands, false);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении пользовательских команд");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить админские команды
    /// </summary>
    /// <returns>Список админских команд</returns>
    [HttpGet("admin")]
    public ActionResult<string> GetAdminCommands()
    {
        try
        {
            var commands = commandService.GetCommandsList("api_user", commandService.UserCommands, commandService.AdminCommands, true);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении админских команд");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить пользовательские команды для определенной платформы
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>Список пользовательских команд для платформы</returns>
    [HttpGet("user/platform/{platform}")]
    public async Task<ActionResult<string[]>> GetUserCommandsForPlatform(Platform platform)
    {
        try
        {
            var commands = await commandService.GetUserCommandsAsync(platform);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении пользовательских команд для платформы {Platform}",
                platform
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить админские команды для определенной платформы
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>Список админских команд для платформы</returns>
    [HttpGet("admin/platform/{platform}")]
    public async Task<ActionResult<string[]>> GetAdminCommandsForPlatform(Platform platform)
    {
        try
        {
            var commands = await commandService.GetAdminCommandsAsync(platform);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении админских команд для платформы {Platform}",
                platform
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить детальную информацию о пользовательских командах для платформы
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>Детальная информация о пользовательских командах</returns>
    [HttpGet("user/platform/{platform}/info")]
    public async Task<ActionResult<CommandInfo[]>> GetUserCommandsInfoForPlatform(Platform platform)
    {
        try
        {
            var commands = await commandService.GetUserCommandsInfoAsync(platform);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении информации о пользовательских командах для платформы {Platform}",
                platform
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить детальную информацию об админских командах для платформы
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>Детальная информация об админских командах</returns>
    [HttpGet("admin/platform/{platform}/info")]
    public async Task<ActionResult<CommandInfo[]>> GetAdminCommandsInfoForPlatform(
        Platform platform
    )
    {
        try
        {
            var commands = await commandService.GetAdminCommandsInfoAsync(platform);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении информации об админских командах для платформы {Platform}",
                platform
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить параметры команды
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>Параметры команды</returns>
    [HttpGet("{commandName}/parameters")]
    public async Task<ActionResult<CommandParameterInfo[]>> GetCommandParameters(string commandName)
    {
        try
        {
            var parameters = await commandService.GetCommandParametersAsync(commandName);
            return Ok(parameters);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении параметров команды {CommandName}",
                commandName
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Выполнить команду
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <param name="input">Входные параметры</param>
    /// <returns>Результат выполнения команды</returns>
    [HttpPost("{commandName}/execute")]
    public async Task<ActionResult<string>> ExecuteCommand(
        string commandName,
        [FromBody] string input
    )
    {
        try
        {
            var result = await commandService.ExecuteCommandAsync(commandName, input);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выполнении команды {CommandName}", commandName);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
