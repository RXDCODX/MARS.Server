using MARS.Server.Services.TelegramBotService;
using Microsoft.AspNetCore.Mvc;

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
    /// <returns>Результат операции</returns>
    [HttpPost("relogin")]
    public async Task<IActionResult> ReLogin(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Запрос на принудительную переавторизацию WTelegram");

            await clientService.ReLoginAsync(cancellationToken);

            return Ok(new { success = true, message = "Переавторизация выполнена успешно" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при переавторизации WTelegram");

            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Ошибка при переавторизации",
                    error = ex.Message,
                }
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
            var client = await clientService.GetClientAsync(cancellationToken);

            return Ok(
                new
                {
                    isAuthenticated = client.User != null,
                    userId = client.User?.id,
                    username = client.User?.username,
                    phone = client.User?.phone,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статуса WTelegram");

            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Ошибка при получении статуса",
                    error = ex.Message,
                }
            );
        }
    }
}
