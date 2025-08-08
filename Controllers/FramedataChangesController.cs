using MARS.Server.Services.Framedata;
using MARS.Server.Services.Framedata.Entitys;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для управления изменениями в фреймдате
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FramedataChangesController(
    FramedataChangeDetectionService changeDetectionService,
    ILogger<FramedataChangesController> logger
) : ControllerBase
{
    /// <summary>
    /// Получает список ожидающих изменений
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<List<FramedataChange>>> GetPendingChanges()
    {
        try
        {
            var changes = await changeDetectionService.GetPendingChanges();
            return Ok(changes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении ожидающих изменений");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Применяет изменение
    /// </summary>
    [HttpPost("apply/{changeId}")]
    public async Task<ActionResult> ApplyChange(int changeId)
    {
        try
        {
            await changeDetectionService.ApplyChange(changeId);
            return Ok(new { message = "Изменение успешно применено" });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(
                ex,
                "Попытка применить несуществующее изменение {ChangeId}",
                changeId
            );
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(
                ex,
                "Попытка применить изменение с неподходящим статусом {ChangeId}",
                changeId
            );
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при применении изменения {ChangeId}", changeId);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Отклоняет изменение
    /// </summary>
    [HttpPost("reject/{changeId}")]
    public async Task<ActionResult> RejectChange(int changeId)
    {
        try
        {
            await changeDetectionService.RejectChange(changeId);
            return Ok(new { message = "Изменение отклонено" });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(
                ex,
                "Попытка отклонить несуществующее изменение {ChangeId}",
                changeId
            );
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отклонении изменения {ChangeId}", changeId);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Запускает обнаружение изменений
    /// </summary>
    [HttpPost("detect")]
    public async Task<ActionResult> StartDetection()
    {
        try
        {
            await changeDetectionService.StartScrupFrameData();
            return Ok(new { message = "Обнаружение изменений запущено" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при запуске обнаружения изменений");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получает статистику изменений
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStats()
    {
        try
        {
            var pendingChanges = await changeDetectionService.GetPendingChanges();

            var stats = new
            {
                TotalPending = pendingChanges.Count,
                ByType = pendingChanges
                    .GroupBy(c => c.ChangeType)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                ByCharacter = pendingChanges
                    .GroupBy(c => c.CharacterName)
                    .ToDictionary(g => g.Key, g => g.Count()),
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статистики изменений");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
