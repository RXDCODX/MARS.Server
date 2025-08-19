using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для работы с MediaInfo (Alerts)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MediaInfoApiController(
    IDbContextFactory<AppDbContext> factory,
    ILogger<MediaInfoApiController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить все алерты
    /// </summary>
    /// <returns>Список всех алертов</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApiMediaInfo>>> GetAllAlerts()
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var alerts = await dbContext.Alerts.ToListAsync();
            var apiAlerts = alerts.Select(a => new ApiMediaInfo(a)).ToList();
            return Ok(apiAlerts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении алертов");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить алерт по ID
    /// </summary>
    /// <param name="id">ID алерта</param>
    /// <returns>Алерт</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiMediaInfo>> GetAlert(Guid id)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var alert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == id);

            return alert == null
                ? NotFound($"Алерт с ID '{id}' не найден")
                : Ok(new ApiMediaInfo(alert));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении алерта {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить файл алерта по ID
    /// </summary>
    /// <param name="id">ID алерта</param>
    /// <returns>Файл алерта</returns>
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> GetAlertFile(Guid id)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var alert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == id);
            if (alert == null)
            {
                return NotFound($"Алерт с ID '{id}' не найден");
            }

            var filePath = alert.FileInfo.FilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                return NotFound("Путь к файлу не найден");
            }

            // Если файл находится в памяти, нужно получить его из MemoryStorage
            if (filePath.StartsWith("memory/"))
            {
                // TODO: Реализовать получение файла из MemoryStorage
                return NotFound("Файлы в памяти пока не поддерживаются");
            }

            // Для локальных файлов
            var fullPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                filePath.TrimStart('/')
            );

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound($"Файл не найден по пути: {fullPath}");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var contentType = GetContentType(alert.FileInfo.Extension);

            return File(fileBytes, contentType, alert.FileInfo.FileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении файла алерта {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Определяет MIME-тип файла по расширению
    /// </summary>
    /// <param name="extension">Расширение файла</param>
    /// <returns>MIME-тип</returns>
    private static string GetContentType(string extension)
    {
        return extension.ToLower() switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }

    /// <summary>
    /// Создать новый алерт
    /// </summary>
    /// <param name="alert">Данные алерта</param>
    /// <returns>Созданный алерт</returns>
    [HttpPost]
    public async Task<ActionResult<ApiMediaInfo>> CreateAlert([FromBody] ApiMediaInfo alert)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            // ApiMediaInfo наследуется от MediaInfo, поэтому можно использовать напрямую
            dbContext.Alerts.Add(alert);
            await dbContext.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetAlert),
                new { id = alert.Id },
                new ApiMediaInfo(alert)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании алерта");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Обновить существующий алерт
    /// </summary>
    /// <param name="id">ID алерта</param>
    /// <param name="alert">Обновленные данные алерта</param>
    /// <returns>Обновленный алерт</returns>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiMediaInfo>> UpdateAlert(
        Guid id,
        [FromBody] ApiMediaInfo alert
    )
    {
        try
        {
            if (id != alert.Id)
            {
                return BadRequest("ID в URL не совпадает с ID в теле запроса");
            }

            await using var dbContext = await factory.CreateDbContextAsync();

            var existingAlert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == id);
            if (existingAlert == null)
            {
                return NotFound($"Алерт с ID '{id}' не найден");
            }

            // Создаем новый объект MediaInfo с обновленными данными
            var updatedAlert = new MediaInfo
            {
                Id = existingAlert.Id,
                TextInfo = alert.TextInfo,
                FileInfo = alert.FileInfo,
                PositionInfo = alert.PositionInfo,
                MetaInfo = alert.MetaInfo,
                StylesInfo = alert.StylesInfo,
            };

            dbContext.Entry(existingAlert).State = EntityState.Detached;

            dbContext.Alerts.Update(updatedAlert);

            await dbContext.SaveChangesAsync();

            return Ok(new ApiMediaInfo(updatedAlert));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении алерта {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Удалить алерт
    /// </summary>
    /// <param name="id">ID алерта</param>
    /// <returns>Результат операции</returns>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAlert(Guid id)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var alert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == id);
            if (alert == null)
            {
                return NotFound($"Алерт с ID '{id}' не найден");
            }

            dbContext.Alerts.Remove(alert);
            await dbContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении алерта {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
