using MARS.Server.Services.CommandExecutor.Adapters;
using Microsoft.Extensions.Logging;

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
    public ActionResult<OperationResult<string>> GetUserCommands()
    {
        ActionResult<OperationResult<string>> result;
        try
        {
            var commands = commandService.GetCommandsList(
                "api_user",
                commandService.UserCommands,
                commandService.AdminCommands,
                false
            );
            result = Ok(OperationResult<string>.Ok("Получены пользовательские команды", commands));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении пользовательских команд");
            result = Ok(
                OperationResult<string>.Bad(
                    "Ошибка при получении пользовательских команд",
                    string.Empty
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить админские команды
    /// </summary>
    /// <returns>Список админских команд</returns>
    [HttpGet("admin")]
    public ActionResult<OperationResult<string>> GetAdminCommands()
    {
        ActionResult<OperationResult<string>> result;
        try
        {
            var commands = commandService.GetCommandsList(
                "api_user",
                commandService.UserCommands,
                commandService.AdminCommands,
                true
            );
            result = Ok(OperationResult<string>.Ok("Получены админские команды", commands));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении админских команд");
            result = Ok(
                OperationResult<string>.Bad("Ошибка при получении админских команд", string.Empty)
            );
        }

        return result;
    }

    /// <summary>
    /// Получить пользовательские команды для определенной платформы
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>Список пользовательских команд для платформы</returns>
    [HttpGet("user/platform/{platform}")]
    public ActionResult<OperationResult<string[]>> GetUserCommandsForPlatform(Platform platform)
    {
        ActionResult<OperationResult<string[]>> result;
        try
        {
            var commands = commandService.GetUserCommands(platform);
            result = Ok(
                OperationResult<string[]>.Ok(
                    "Получены пользовательские команды для платформы",
                    commands
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении пользовательских команд для платформы {Platform}",
                platform
            );
            result = Ok(
                OperationResult<string[]>.Bad("Ошибка при получении пользовательских команд", [])
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
    public ActionResult<OperationResult<string[]>> GetAdminCommandsForPlatform(Platform platform)
    {
        ActionResult<OperationResult<string[]>> result;
        try
        {
            var commands = commandService.GetAdminCommands(platform);
            result = Ok(
                OperationResult<string[]>.Ok("Получены админские команды для платформы", commands)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении админских команд для платформы {Platform}",
                platform
            );
            result = Ok(OperationResult<string[]>.Bad("Ошибка при получении админских команд", []));
        }

        return result;
    }

    /// <summary>
    /// Получить детальную информацию о пользовательских командах для платформы
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>Детальная информация о пользовательских командах</returns>
    [HttpGet("user/platform/{platform}/info")]
    public ActionResult<OperationResult<BaseCommand[]>> GetUserCommandsInfoForPlatform(
        Platform platform
    )
    {
        ActionResult<OperationResult<BaseCommand[]>> result;
        try
        {
            var commands = commandService.GetUserCommandsInfo(platform);
            result = Ok(
                OperationResult<BaseCommand[]>.Ok(
                    "Получена информация о пользовательских командах",
                    commands
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении информации о пользовательских командах для платформы {Platform}",
                platform
            );
            result = Ok(
                OperationResult<BaseCommand[]>.Bad("Ошибка при получении информации о командах", [])
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
    public ActionResult<OperationResult<BaseCommand[]>> GetAdminCommandsInfoForPlatform(
        Platform platform
    )
    {
        ActionResult<OperationResult<BaseCommand[]>> result;
        try
        {
            var commands = commandService.GetAdminCommandsInfo(platform);
            result = Ok(
                OperationResult<BaseCommand[]>.Ok(
                    "Получена информация об админских командах",
                    commands
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении информации об админских командах для платформы {Platform}",
                platform
            );
            result = Ok(
                OperationResult<BaseCommand[]>.Bad(
                    "Ошибка при получении информации об админских командах",
                    []
                )
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
    public ActionResult<OperationResult<CommandParameterInfo[]>> GetCommandParameters(
        string commandName
    )
    {
        ActionResult<OperationResult<CommandParameterInfo[]>> result;
        try
        {
            var parameters = commandService.GetCommandParameters(commandName);

            result = Ok(
                OperationResult<CommandParameterInfo[]>.Ok(
                    "Получены параметры команды",
                    parameters ?? []
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении параметров команды {CommandName}",
                commandName
            );
            result = Ok(
                OperationResult<CommandParameterInfo[]>.Bad(
                    "Ошибка при получении параметров команды",
                    []
                )
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
    public async Task<ActionResult<OperationResult<string>>> ExecuteCommand(
        string commandName,
        [FromBody] string input
    )
    {
        ActionResult<OperationResult<string>> result;
        try
        {
            var commandResult = await commandService.ExecuteCommandAsync(commandName, input);
            result = Ok(OperationResult<string>.Ok("Команда выполнена", commandResult));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выполнении команды {CommandName}", commandName);
            result = Ok(OperationResult<string>.Bad("Ошибка при выполнении команды", string.Empty));
        }

        return result;
    }
}
