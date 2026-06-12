using System;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.Telegram.BotService.Entities;
using MARS.Server.Services.Telegram.WTelegram;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для управления WTelegram клиентом
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WTelegramController(
    WTelegramClientService clientService,
    ILogger<WTelegramController> logger
) : ControllerBase
{
    /// <summary>
    /// Принудительно выполняет повторную авторизацию WTelegram клиента
    /// </summary>
    /// <returns>Результат операции переавторизации</returns>
    [HttpPost("relogin")]
    public async Task<IActionResult> ReLogin(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Запрос на принудительную переавторизацию WTelegram");

            await clientService.ReLoginAsync(cancellationToken);

            var status = await clientService.GetClientStatusAsync(cancellationToken);

            return Ok(
                WTelegramOperationResult.CreateSuccess("Переавторизация выполнена успешно", status)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при переавторизации WTelegram");

            return StatusCode(
                500,
                WTelegramOperationResult.CreateFailure("Ошибка при переавторизации", ex.Message)
            );
        }
    }

    /// <summary>
    /// Проверяет статус авторизации WTelegram клиента
    /// </summary>
    /// <returns>Информация о статусе клиента</returns>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var status = await clientService.GetClientStatusAsync(cancellationToken);

            return Ok(status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статуса WTelegram");

            return StatusCode(
                500,
                WTelegramOperationResult.CreateFailure("Ошибка при получении статуса", ex.Message)
            );
        }
    }
}
