using MARS.Server.CustomLoggers.DatabaseLogger;
using MARS.Server.Services.Logs.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для работы с логами
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LogsController(ILogsService logsService, ILogger<LogsController> logger)
    : ControllerBase
{
    /// <summary>
    /// Получить логи с пагинацией и фильтрами
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(LogResponse), 200)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = "whenlogged",
        [FromQuery] bool sortDescending = true,
        [FromQuery] LogLevel? logLevel = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? searchText = null
    )
    {
        try
        {
            logger.LogInformation("Получен запрос логов: page={Page}, pageSize={PageSize}, logLevel={LogLevel}, fromDate={FromDate}, toDate={ToDate}, searchText={SearchText}", 
                page, pageSize, logLevel, fromDate, toDate, searchText);
            if (page < 1)
            {
                return BadRequest("Номер страницы должен быть больше 0");
            }

            if (pageSize < 1 || pageSize > 1000)
            {
                return BadRequest("Размер страницы должен быть от 1 до 1000");
            }

            var (logs, totalCount) = await logsService.GetLogsAsync(
                page,
                pageSize,
                sortBy,
                sortDescending,
                logLevel,
                fromDate,
                toDate,
                searchText
            );

            var response = new LogResponse
            {
                Logs = logs,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            };

            logger.LogInformation("Возвращаем {LogCount} логов из {TotalCount} общих", logs.Count(), totalCount);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении логов");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить логи по уровню логирования
    /// </summary>
    [HttpGet("by-level/{logLevel}")]
    [ProducesResponseType(typeof(IEnumerable<Log>), 200)]
    public async Task<IActionResult> GetLogsByLevel(LogLevel logLevel)
    {
        try
        {
            var logs = await logsService.GetLogsByLevelAsync(logLevel);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении логов по уровню {LogLevel}", logLevel);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить логи за период
    /// </summary>
    [HttpGet("by-date-range")]
    [ProducesResponseType(typeof(IEnumerable<Log>), 200)]
    public async Task<IActionResult> GetLogsByDateRange(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate
    )
    {
        try
        {
            if (fromDate > toDate)
            {
                return BadRequest("Дата начала должна быть меньше или равна дате окончания");
            }

            var logs = await logsService.GetLogsByDateRangeAsync(fromDate, toDate);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении логов за период с {FromDate} по {ToDate}",
                fromDate,
                toDate
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить последние логи
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IEnumerable<Log>), 200)]
    public async Task<IActionResult> GetRecentLogs([FromQuery] int count = 100)
    {
        try
        {
            if (count < 1 || count > 1000)
            {
                return BadRequest("Количество логов должно быть от 1 до 1000");
            }

            var logs = await logsService.GetRecentLogsAsync(count);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении последних {Count} логов", count);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить статистику по логам
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(LogsStatistics), 200)]
    public async Task<IActionResult> GetLogsStatistics()
    {
        try
        {
            var statistics = await logsService.GetLogsStatisticsAsync();
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статистики логов");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Создать тестовый лог для проверки работы системы
    /// </summary>
    [HttpPost("test")]
    public IActionResult CreateTestLog()
    {
        try
        {
            logger.LogTrace("Тестовый лог уровня Trace");
            logger.LogDebug("Тестовый лог уровня Debug");
            logger.LogInformation("Тестовый лог уровня Information");
            logger.LogWarning("Тестовый лог уровня Warning");
            logger.LogError("Тестовый лог уровня Error");
            logger.LogCritical("Тестовый лог уровня Critical");
            
            return Ok(new { message = "Тестовые логи созданы", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании тестовых логов");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}

/// <summary>
/// Ответ с логами и информацией о пагинации
/// </summary>
public class LogResponse
{
    public IEnumerable<Log> Logs { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
