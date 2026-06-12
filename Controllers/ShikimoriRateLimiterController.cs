using System;
using MARS.Server.Services;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Shikimori.Entitys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    public ActionResult<OperationResult<RateLimiterInfo?>> GetRateLimiterInfo()
    {
        ActionResult<OperationResult<RateLimiterInfo?>> result;
        try
        {
            var info = shikimoriService.GetRateLimiterInfo();
            result = Ok(
                OperationResult<RateLimiterInfo?>.Ok("Информация о рейт лимитере получена", info)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении информации о рейт лимитере");
            result = Ok(
                OperationResult<RateLimiterInfo?>.Bad(
                    "Ошибка при получении информации о рейт лимитере",
                    null
                )
            );
        }

        return result;
    }
}
