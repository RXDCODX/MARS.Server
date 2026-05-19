using MARS.Server.Services;
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
    public async Task<ActionResult<OperationResult<List<ApiMediaInfo>>>> GetAllAlerts()
    {
        ActionResult<OperationResult<List<ApiMediaInfo>>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var alerts = await dbContext.Alerts.ToListAsync();
            var apiAlerts = alerts.Select(a => new ApiMediaInfo(a)).ToList();
            result = Ok(OperationResult<List<ApiMediaInfo>>.Ok("Получены все алерты", apiAlerts));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении алертов");
            result = Ok(
                OperationResult<List<ApiMediaInfo>>.Bad("Ошибка при получении алертов", [])
            );
        }

        return result;
    }

    /// <summary>
    /// Получить алерт по ID
    /// </summary>
    /// <param name="id">ID алерта</param>
    /// <returns>Алерт</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperationResult<ApiMediaInfo?>>> GetAlert(Guid id)
    {
        ActionResult<OperationResult<ApiMediaInfo?>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var alert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == id);

            if (alert != null)
            {
                result = Ok(
                    OperationResult<ApiMediaInfo?>.Ok("Алерт найден", new ApiMediaInfo(alert))
                );
            }
            else
            {
                result = Ok(
                    OperationResult<ApiMediaInfo?>.Bad($"Алерт с ID '{id}' не найден", null)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении алерта {Id}", id);
            result = Ok(OperationResult<ApiMediaInfo?>.Bad("Ошибка при получении алерта", null));
        }

        return result;
    }

    /// <summary>
    /// Получить файл алерта по ID
    /// </summary>
    /// <param name="id">ID алерта</param>
    /// <returns>Файл алерта</returns>
    [HttpGet("{id:guid}/file")]
    public async Task<ActionResult> GetAlertFile(Guid id)
    {
        ActionResult result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var alert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == id);
            if (alert == null)
            {
                result = NotFound($"Алерт с ID '{id}' не найден");
            }
            else
            {
                var filePath = alert.FileInfo.FilePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    result = NotFound("Путь к файлу не найден");
                }
                else if (filePath.StartsWith("memory/"))
                {
                    // TODO: Реализовать получение файла из MemoryStorage
                    result = NotFound("Файлы в памяти пока не поддерживаются");
                }
                else
                {
                    // Для локальных файлов
                    var fullPath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        filePath.TrimStart('/')
                    );

                    if (!System.IO.File.Exists(fullPath))
                    {
                        result = NotFound($"Файл не найден по пути: {fullPath}");
                    }
                    else
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                        var contentType = GetContentType(alert.FileInfo.Extension);
                        result = File(fileBytes, contentType, alert.FileInfo.FileName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении файла алерта {Id}", id);
            result = StatusCode(500, "Внутренняя ошибка сервера");
        }

        return result;
    }

    /// <summary>
    /// Загрузить файл для алерта (возвращает информацию о файле)
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult<OperationResult<object>>> UploadFile()
    {
        try
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null)
            {
                return Ok(OperationResult<object>.Bad("Файл не передан"));
            }

            var originalName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(originalName);

            var generated = $"{Guid.NewGuid()}{extension}";
            var relative = Path.Combine("media", "uploads", generated).Replace('\\', '/');
            var fullPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                relative.Replace('/', Path.DirectorySeparatorChar)
            );

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var fileInfo = new
            {
                fileName = originalName,
                extension = extension,
                filePath = "/" + relative,
                isLocalFile = true,
            };

            return Ok(OperationResult<object>.Ok("Файл загружен", fileInfo));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при загрузке файла");
            return StatusCode(500, "Ошибка при загрузке файла");
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
    public async Task<ActionResult<OperationResult<ApiMediaInfo?>>> CreateAlert(
        [FromBody] ApiMediaInfo alert
    )
    {
        ActionResult<OperationResult<ApiMediaInfo?>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            // ApiMediaInfo наследуется от MediaInfo, поэтому можно использовать напрямую
            dbContext.Alerts.Add(alert);
            await dbContext.SaveChangesAsync();

            result = Ok(
                OperationResult<ApiMediaInfo?>.Ok("Алерт успешно создан", new ApiMediaInfo(alert))
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании алерта");
            result = Ok(OperationResult<ApiMediaInfo?>.Bad("Ошибка при создании алерта", null));
        }

        return result;
    }

    /// <summary>
    /// Обновить существующий алерт
    /// </summary>
    /// <param name="id">ID алерта</param>
    /// <param name="alert">Обновленные данные алерта</param>
    /// <returns>Обновленный алерт</returns>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OperationResult<ApiMediaInfo?>>> UpdateAlert(
        Guid id,
        [FromBody] ApiMediaInfo alert
    )
    {
        ActionResult<OperationResult<ApiMediaInfo?>> result = null!;

        try
        {
            if (id != alert.Id)
            {
                result = Ok(
                    OperationResult<ApiMediaInfo?>.Bad(
                        "ID в URL не совпадает с ID в теле запроса",
                        null
                    )
                );
            }
            else
            {
                await using var dbContext = await factory.CreateDbContextAsync();

                var existingAlert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == id);
                if (existingAlert == null)
                {
                    result = Ok(
                        OperationResult<ApiMediaInfo?>.Bad($"Алерт с ID '{id}' не найден", null)
                    );
                }
                else
                {
                    var oldPath = existingAlert.FileInfo.FilePath ?? string.Empty;
                    var newPath = alert.FileInfo.FilePath ?? string.Empty;

                    if (
                        !string.IsNullOrWhiteSpace(oldPath)
                        && !string.IsNullOrWhiteSpace(newPath)
                        && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)
                        && !oldPath.StartsWith("memory/", StringComparison.OrdinalIgnoreCase)
                        && !newPath.StartsWith("memory/", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        var sourceRelativePath = oldPath
                            .TrimStart('/')
                            .Replace('/', Path.DirectorySeparatorChar);
                        var targetRelativePath = newPath
                            .TrimStart('/')
                            .Replace('/', Path.DirectorySeparatorChar);
                        var baseRoots = new[]
                        {
                            Directory.GetCurrentDirectory(),
                            AppContext.BaseDirectory,
                        };

                        var sourceRoot = baseRoots.FirstOrDefault(root =>
                            System.IO.File.Exists(Path.Combine(root, "wwwroot", sourceRelativePath))
                        );

                        if (string.IsNullOrWhiteSpace(sourceRoot))
                        {
                            result = Ok(
                                OperationResult<ApiMediaInfo?>.Bad(
                                    "Файл для перемещения не найден",
                                    null
                                )
                            );
                            return result;
                        }

                        var oldFullPath = Path.Combine(sourceRoot, "wwwroot", sourceRelativePath);
                        var newFullPath = Path.Combine(sourceRoot, "wwwroot", targetRelativePath);

                        var newDirectory = Path.GetDirectoryName(newFullPath);
                        if (!string.IsNullOrWhiteSpace(newDirectory))
                        {
                            Directory.CreateDirectory(newDirectory);
                        }

                        System.IO.File.Move(oldFullPath, newFullPath, true);
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

                    result = Ok(
                        OperationResult<ApiMediaInfo?>.Ok(
                            "Алерт успешно обновлен",
                            new ApiMediaInfo(updatedAlert)
                        )
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении алерта {Id}", id);
            result = Ok(OperationResult<ApiMediaInfo?>.Bad("Ошибка при обновлении алерта", null));
        }

        return result;
    }

    /// <summary>
    /// Удалить алерт
    /// </summary>
    /// <param name="id">ID алерта</param>
    /// <returns>Результат операции</returns>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<OperationResult>> DeleteAlert(Guid id)
    {
        ActionResult<OperationResult> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var alert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == id);
            if (alert == null)
            {
                result = Ok(OperationResult.Bad($"Алерт с ID '{id}' не найден"));
            }
            else
            {
                dbContext.Alerts.Remove(alert);
                await dbContext.SaveChangesAsync();
                result = Ok(OperationResult.Ok("Алерт успешно удален"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении алерта {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при удалении алерта"));
        }

        return result;
    }
}
