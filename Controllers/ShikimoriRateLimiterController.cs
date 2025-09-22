using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Shikimori.Entitys;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для мониторинга состояния рейт лимитера Shikimori API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ShikimoriRateLimiterController(
    ShikimoriService shikimoriService,
    ILogger<ShikimoriRateLimiterController> logger
) : ControllerBase
{
    /// <summary>
    /// Получает информацию о текущем состоянии рейт лимитера
    /// </summary>
    /// <returns>Информация о доступных слотах и времени сброса лимитов</returns>
    [HttpGet("info")]
    public ActionResult<RateLimiterInfo> GetRateLimiterInfo()
    {
        var result = new RateLimiterInfo();

        try
        {
            result = shikimoriService.GetRateLimiterInfo();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении информации о рейт лимитере");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }

        return Ok(result);
    }
}
