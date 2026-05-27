using System.Text.Json;
using MARS.Server.Services;
using MARS.Server.Services.Media;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для работы с MediaInfo (Alerts)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MediaInfoApiController(
    IDbContextFactory<AppDbContext> factory,
    ILogger<MediaInfoApiController> logger,
    IMediaFileStorageService storage,
    IMediaInspector inspector,
    IMediaTranscoder transcoder,
    IWebHostEnvironment webHostEnvironment
) : ControllerBase
{
    private static readonly JsonSerializerOptions FormJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(),
        },
    };

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
        [FromForm] MediaInfoUpsertRequest request
    )
    {
        ActionResult<OperationResult<ApiMediaInfo?>> result = null!;

        try
        {
            if (string.IsNullOrWhiteSpace(request.AlertJson))
            {
                result = Ok(OperationResult<ApiMediaInfo?>.Bad("Данные алерта не переданы", null));
            }
            else
            {
                var alert = JsonSerializer.Deserialize<ApiMediaInfo>(request.AlertJson, FormJsonOptions);
                if (alert is null)
                {
                    result = Ok(OperationResult<ApiMediaInfo?>.Bad("Не удалось разобрать алерт", null));
                }
                else if (request.File is null)
                {
                    result = Ok(OperationResult<ApiMediaInfo?>.Bad("Файл не передан", null));
                }
                else if (!TryResolveUploadedMemsFilePath(alert.FileInfo.FilePath, out var targetFilePath, out var pathError))
                {
                    result = Ok(OperationResult<ApiMediaInfo?>.Bad(pathError, null));
                }
                else
                {
                    var fileInfo = await storage.SaveFileAsync(request.File, targetFilePath);

                    try
                    {
                        var fullPath = Path.Combine(
                            webHostEnvironment.WebRootPath,
                            fileInfo.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                        );

                        // Ensure playable: transcode if needed
                        var playablePath = await transcoder.EnsurePlayableAsync(fullPath);

                        if (!string.Equals(playablePath, fullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            var rel = "/" + Path.GetRelativePath(webHostEnvironment.WebRootPath, Path.GetFullPath(playablePath)).Replace('\\', '/');
                            fileInfo.FilePath = rel;
                            fileInfo.Extension = Path.GetExtension(playablePath);
                            fileInfo.FileName = Path.GetFileName(playablePath);
                            fileInfo.Type = await fileInfo.Extension.GetFileMediaTypeAsync();

                            // ensure dev copies for transcoded file
                            try
                            {
                                await storage.CopyToDevCopiesAsync(fileInfo.FilePath);
                            }
                            catch { }
                        }
                        else
                        {
                            var probe = await inspector.ProbeAsync(fullPath);
                            if ((fileInfo.Type == MediaType.Audio && (probe.BitrateKbps is null || probe.BitrateKbps < 128)) || (fileInfo.Type == MediaType.Video && (probe.BitrateKbps is null || probe.BitrateKbps < 128)))
                            {
                                logger.LogInformation("Загружен файл с низким битрейтом: {File} ({Bitrate} kbps)", fullPath, probe.BitrateKbps);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Не удалось выполнить пробинг/транскодирование файла {File}", fileInfo.FilePath);
                    }

                    if (fileInfo.IsLocalFile)
                    {
                        try
                        {
                            await storage.CopyToDevCopiesAsync(fileInfo.FilePath);
                        }
                        catch (Exception copyEx)
                        {
                            logger.LogDebug(copyEx, "Не удалось синхронизировать dev-копию файла {File}", fileInfo.FilePath);
                        }
                    }
                    var createdAlert = CreateStoredAlert(alert, fileInfo);

                    await using var dbContext = await factory.CreateDbContextAsync();

                    dbContext.Alerts.Add(createdAlert);
                    await dbContext.SaveChangesAsync();

                    result = Ok(
                        OperationResult<ApiMediaInfo?>.Ok(
                            "Алерт успешно создан",
                            new ApiMediaInfo(createdAlert)
                        )
                    );
                }
            }
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
        [FromForm] MediaInfoUpsertRequest request
    )
    {
        ActionResult<OperationResult<ApiMediaInfo?>> result = null!;

        try
        {
            if (string.IsNullOrWhiteSpace(request.AlertJson))
            {
                result = Ok(OperationResult<ApiMediaInfo?>.Bad("Данные алерта не переданы", null));
            }
            else
            {
                var alert = JsonSerializer.Deserialize<ApiMediaInfo>(request.AlertJson, FormJsonOptions);
                if (alert is null)
                {
                    result = Ok(OperationResult<ApiMediaInfo?>.Bad("Не удалось разобрать алерт", null));
                }
                else if (id != alert.Id)
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
                            OperationResult<ApiMediaInfo?>.Bad(
                                $"Алерт с ID '{id}' не найден",
                                null
                            )
                        );
                    }
                    else
                    {
                        var resolvedFileInfo = alert.FileInfo;

                        if (request.File is not null)
                        {
                            if (!TryResolveUploadedMemsFilePath(alert.FileInfo.FilePath, out var targetFilePath, out var pathError))
                            {
                                result = Ok(OperationResult<ApiMediaInfo?>.Bad(pathError, null));
                                return result;
                            }

                            resolvedFileInfo = await storage.SaveFileAsync(request.File, targetFilePath);

                            try
                            {
                                var fullPath = Path.Combine(
                                    webHostEnvironment.WebRootPath,
                                    resolvedFileInfo.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                                );

                                var playable = await transcoder.EnsurePlayableAsync(fullPath);
                                if (!string.Equals(playable, fullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    var rel = "/" + Path.GetRelativePath(webHostEnvironment.WebRootPath, Path.GetFullPath(playable)).Replace('\\', '/');
                                    resolvedFileInfo.FilePath = rel;
                                    resolvedFileInfo.Extension = Path.GetExtension(playable);
                                    resolvedFileInfo.FileName = Path.GetFileName(playable);
                                    resolvedFileInfo.Type = await resolvedFileInfo.Extension.GetFileMediaTypeAsync();

                                    try { await storage.CopyToDevCopiesAsync(resolvedFileInfo.FilePath); } catch { }
                                }
                                else
                                {
                                    var probe = await inspector.ProbeAsync(fullPath);
                                    if ((resolvedFileInfo.Type == MediaType.Audio && (probe.BitrateKbps is null || probe.BitrateKbps < 128)) || (resolvedFileInfo.Type == MediaType.Video && (probe.BitrateKbps is null || probe.BitrateKbps < 128)))
                                    {
                                        logger.LogInformation("Загружен файл с низким битрейтом: {File} ({Bitrate} kbps)", fullPath, probe.BitrateKbps);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogDebug(ex, "Не удалось выполнить пробинг/транскодирование файла {File}", resolvedFileInfo.FilePath);
                            }

                            try
                            {
                                await storage.CopyToDevCopiesAsync(resolvedFileInfo.FilePath);
                            }
                            catch (Exception copyEx)
                            {
                                logger.LogDebug(copyEx, "Не удалось синхронизировать dev-копию файла {File}", resolvedFileInfo.FilePath);
                            }

                            var oldPath = existingAlert.FileInfo.FilePath ?? string.Empty;
                            if (
                                !string.IsNullOrWhiteSpace(oldPath)
                                && !oldPath.StartsWith("memory/", StringComparison.OrdinalIgnoreCase)
                                && oldPath.StartsWith("/", StringComparison.Ordinal)
                            )
                            {
                                var oldFullPath = Path.Combine(
                                    Directory.GetCurrentDirectory(),
                                    "wwwroot",
                                    oldPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                                );

                                if (System.IO.File.Exists(oldFullPath))
                                {
                                    System.IO.File.Delete(oldFullPath);
                                }
                            }
                        }
                        else
                        {
                            var oldPath = existingAlert.FileInfo.FilePath ?? string.Empty;
                            var newPath = resolvedFileInfo.FilePath ?? string.Empty;

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
                                    System.IO.File.Exists(
                                        Path.Combine(root, "wwwroot", sourceRelativePath)
                                    )
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
                        }

                        var updatedAlert = CreateStoredAlert(alert, resolvedFileInfo);

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении алерта {Id}", id);
            result = Ok(OperationResult<ApiMediaInfo?>.Bad("Ошибка при обновлении алерта", null));
        }

        return result;
    }

    private static bool TryResolveUploadedMemsFilePath(string? filePath, out string resolvedPath, out string errorMessage)
    {
        resolvedPath = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "Укажи путь к файлу внутри Alerts/uploaded_mems";
            return false;
        }

        var normalized = NormalizeRelativePath(filePath);
        var relative = normalized.TrimStart('/');

        if (Path.IsPathRooted(relative) || relative.Contains("..", StringComparison.Ordinal))
        {
            errorMessage = "Путь к файлу должен быть относительным и находиться внутри Alerts/uploaded_mems";
            return false;
        }

        if (!relative.StartsWith("Alerts/uploaded_mems/", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Для загружаемых файлов используй путь внутри Alerts/uploaded_mems";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(relative)))
        {
            errorMessage = "Укажи имя файла в пути Alerts/uploaded_mems";
            return false;
        }

        resolvedPath = normalized;
        return true;
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }

    private static MediaInfo CreateStoredAlert(ApiMediaInfo source, MediaFileInfo fileInfo)
    {
        return new MediaInfo
        {
            Id = source.Id,
            TextInfo = source.TextInfo,
            FileInfo = fileInfo,
            PositionInfo = source.PositionInfo,
            MetaInfo = source.MetaInfo,
            StylesInfo = source.StylesInfo,
        };
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
