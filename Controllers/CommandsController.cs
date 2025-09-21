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
        ActionResult<string> result = StatusCode(500, "Внутренняя ошибка сервера");
        
        try
        {
            var commands = commandService.GetCommandsList(
                "api_user",
                commandService.UserCommands,
                commandService.AdminCommands,
                false
            );
            result = Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении пользовательских команд");
        }
        
        return result;
    }

    /// <summary>
    /// Получить админские команды
    /// </summary>
    /// <returns>Список админских команд</returns>
    [HttpGet("admin")]
    public ActionResult<string> GetAdminCommands()
    {
        ActionResult<string> result = StatusCode(500, "Внутренняя ошибка сервера");
        
        try
        {
            var commands = commandService.GetCommandsList(
                "api_user",
                commandService.UserCommands,
                commandService.AdminCommands,
                true
            );
            result = Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении админских команд");
        }
        
        return result;
    }

    /// <summary>
    /// Получить пользовательские команды для определенной платформы
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>Список пользовательских команд для платформы</returns>
    [HttpGet("user/platform/{platform}")]
    public async Task<ActionResult<string[]>> GetUserCommandsForPlatform(Platform platform)
    {
        ActionResult<string[]> result = StatusCode(500, "Внутренняя ошибка сервера");
        
        try
        {
            var commands = await commandService.GetUserCommandsAsync(platform);
            result = Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении пользовательских команд для платформы {Platform}",
                platform
            );
        }
        
        return result;
    }

    /// <summary>
    /// Получить админские команды для определенной платформы
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>Список админских команд для платформы</returns>
    [HttpGet("admin/platform/{platform}")]
    public async Task<ActionResult<string[]>> GetAdminCommandsForPlatform(Platform platform)
    {
        ActionResult<string[]> result = StatusCode(500, "Внутренняя ошибка сервера");
        
        try
        {
            var commands = await commandService.GetAdminCommandsAsync(platform);
            result = Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении админских команд для платформы {Platform}",
                platform
            );
        }
        
        return result;
    }

    /// <summary>
    /// Получить детальную информацию о пользовательских командах для платформы
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>Детальная информация о пользовательских командах</returns>
    [HttpGet("user/platform/{platform}/info")]
    public async Task<ActionResult<CommandInfo[]>> GetUserCommandsInfoForPlatform(Platform platform)
    {
        ActionResult<CommandInfo[]> result = StatusCode(500, "Внутренняя ошибка сервера");
        
        try
        {
            var commands = await commandService.GetUserCommandsInfoAsync(platform);
            result = Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении информации о пользовательских командах для платформы {Platform}",
                platform
            );
        }
        
        return result;
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
        ActionResult<CommandInfo[]> result = StatusCode(500, "Внутренняя ошибка сервера");
        
        try
        {
            var commands = await commandService.GetAdminCommandsInfoAsync(platform);
            result = Ok(commands);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении информации об админских командах для платформы {Platform}",
                platform
            );
        }
        
        return result;
    }

    /// <summary>
    /// Получить параметры команды
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>Параметры команды</returns>
    [HttpGet("{commandName}/parameters")]
    public async Task<ActionResult<CommandParameterInfo[]>> GetCommandParameters(string commandName)
    {
        ActionResult<CommandParameterInfo[]> result = StatusCode(500, "Внутренняя ошибка сервера");
        
        try
        {
            var parameters = await commandService.GetCommandParametersAsync(commandName);
            result = Ok(parameters);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении параметров команды {CommandName}",
                commandName
            );
        }
        
        return result;
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
        ActionResult<string> result = StatusCode(500, "Внутренняя ошибка сервера");
        
        try
        {
            var commandResult = await commandService.ExecuteCommandAsync(commandName, input);
            result = Ok(commandResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выполнении команды {CommandName}", commandName);
        }
        
        return result;
    }
}
