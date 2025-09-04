using MARS.Server.Services.DatabaseBackup;
using MARS.Server.Services.DatabaseBackup.Models;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для управления резервными копиями базы данных
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DatabaseBackupController(
    IDatabaseBackupService backupService,
    IPgDumpSettingsService pgDumpSettingsService,
    ILogger<DatabaseBackupController> logger
) : ControllerBase
{
    /// <summary>
    /// Создает резервную копию указанной базы данных
    /// </summary>
    /// <param name="databaseName">Имя базы данных (dev или prod)</param>
    /// <returns>Информация о созданной резервной копии</returns>
    [HttpPost("create")]
    public async Task<IActionResult> CreateBackup([FromQuery] string databaseName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return BadRequest("Имя базы данных не может быть пустым");
            }

            var downloadUrl = await backupService.CreateBackupAsync(databaseName);
            var fileName = downloadUrl.Split('/').Last();

            return Ok(
                new CreateBackupResponse
                {
                    Success = true,
                    Message = $"Резервная копия базы данных {databaseName} создана успешно",
                    DownloadUrl = downloadUrl,
                    FileName = fileName,
                    CreatedAt = DateTime.Now,
                }
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new CreateBackupResponse { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при создании резервной копии базы данных {DatabaseName}",
                databaseName
            );
            return StatusCode(
                500,
                new CreateBackupResponse
                {
                    Success = false,
                    Message = "Внутренняя ошибка сервера при создании резервной копии",
                }
            );
        }
    }

    /// <summary>
    /// Скачивает файл резервной копии
    /// </summary>
    /// <param name="fileName">Имя файла резервной копии</param>
    /// <returns>Файл резервной копии для скачивания</returns>
    [HttpGet("download")]
    public async Task<IActionResult> DownloadBackup([FromQuery] string fileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest("Имя файла не может быть пустым");
            }

            var backupStream = await backupService.GetBackupFileAsync(fileName);

            return File(backupStream, "application/sql", fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { success = false, message = "Файл резервной копии не найден" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при скачивании резервной копии {FileName}", fileName);
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Внутренняя ошибка сервера при скачивании резервной копии",
                }
            );
        }
    }

    /// <summary>
    /// Получает список доступных резервных копий
    /// </summary>
    /// <returns>Список файлов резервных копий</returns>
    [HttpGet("list")]
    public async Task<IActionResult> GetBackupsList()
    {
        try
        {
            var backupFileNames = await backupService.GetAvailableBackupsAsync();
            var backupInfo = new List<BackupFileInfo>();

            foreach (var fileName in backupFileNames)
            {
                var fileInfo = await backupService.GetBackupFileInfoAsync(fileName);
                if (fileInfo is not null)
                {
                    backupInfo.Add(fileInfo);
                }
            }

            // Сортируем по времени создания (новые сначала)
            backupInfo = [.. backupInfo.OrderByDescending(f => f.Created)];

            return Ok(
                new BackupListResponse
                {
                    Success = true,
                    Backups = backupInfo,
                    TotalCount = backupInfo.Count,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении списка резервных копий");
            return StatusCode(500, new BackupListResponse { Success = false });
        }
    }

    /// <summary>
    /// Очищает старые резервные копии, оставляя только указанное количество
    /// </summary>
    /// <param name="keepCount">Количество копий для сохранения (по умолчанию 10)</param>
    /// <returns>Результат очистки</returns>
    [HttpPost("cleanup")]
    public async Task<IActionResult> CleanupOldBackups([FromQuery] int keepCount = 10)
    {
        try
        {
            if (keepCount < 1)
            {
                return BadRequest("Количество сохраняемых копий должно быть больше 0");
            }

            var deletedCount = await backupService.CleanupOldBackupsAsync(keepCount);

            return Ok(
                new CleanupBackupsResponse
                {
                    Success = true,
                    Message = $"Очистка завершена. Удалено {deletedCount} старых резервных копий",
                    DeletedCount = deletedCount,
                    KeepCount = keepCount,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при очистке старых резервных копий");
            return StatusCode(
                500,
                new CleanupBackupsResponse
                {
                    Success = false,
                    Message = "Внутренняя ошибка сервера при очистке резервных копий",
                }
            );
        }
    }

    /// <summary>
    /// Получает информацию о состоянии резервного копирования
    /// </summary>
    /// <returns>Статистика резервного копирования</returns>
    [HttpGet("status")]
    public async Task<IActionResult> GetBackupStatus()
    {
        try
        {
            var backupFileNames = await backupService.GetAvailableBackupsAsync();
            var backupFiles = backupFileNames.ToList();

            var totalSize = 0L;
            var oldestBackup = (DateTime?)null;
            var newestBackup = (DateTime?)null;

            if (backupFiles.Count > 0)
            {
                foreach (var fileName in backupFiles)
                {
                    var fileInfo = await backupService.GetBackupFileInfoAsync(fileName);
                    if (fileInfo is not null)
                    {
                        totalSize += fileInfo.Size;

                        if (oldestBackup is null || fileInfo.Created < oldestBackup)
                        {
                            oldestBackup = fileInfo.Created;
                        }

                        if (newestBackup is null || fileInfo.Created > newestBackup)
                        {
                            newestBackup = fileInfo.Created;
                        }
                    }
                }
            }

            return Ok(
                new BackupStatusResponse
                {
                    Success = true,
                    Status = new BackupStatusInfo
                    {
                        TotalBackups = backupFiles.Count,
                        TotalSizeBytes = totalSize,
                        TotalSizeMB = Math.Round(totalSize / (1024.0 * 1024.0), 2),
                        OldestBackup = oldestBackup,
                        NewestBackup = newestBackup,
                        StorageInfo = "MemoryStorage",
                    },
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статуса резервного копирования");
            return StatusCode(500, new BackupStatusResponse { Success = false });
        }
    }

    /// <summary>
    /// Получает текущие настройки pg_dump
    /// </summary>
    /// <returns>Текущие настройки pg_dump</returns>
    [HttpGet("pg-dump/settings")]
    public async Task<IActionResult> GetPgDumpSettings()
    {
        try
        {
            var settings = await pgDumpSettingsService.GetActiveSettingsAsync();
            if (settings == null)
            {
                return Ok(
                    new PgDumpSettingsResponse
                    {
                        Success = false,
                        Message =
                            "Настройки pg_dump не найдены. Пожалуйста, настройте путь к pg_dump.",
                    }
                );
            }

            var validationInfo = await pgDumpSettingsService.ValidatePgDumpPathAsync(
                settings.PgDumpPath
            );

            return Ok(
                new PgDumpSettingsResponse
                {
                    Success = true,
                    Message = "Настройки pg_dump получены успешно",
                    Settings = settings,
                    ValidationInfo = validationInfo,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении настроек pg_dump");
            return StatusCode(
                500,
                new PgDumpSettingsResponse
                {
                    Success = false,
                    Message = "Внутренняя ошибка сервера при получении настроек pg_dump",
                }
            );
        }
    }

    /// <summary>
    /// Обновляет настройки pg_dump
    /// </summary>
    /// <param name="request">Запрос с новыми настройками</param>
    /// <returns>Результат обновления настроек</returns>
    [HttpPost("pg-dump/settings")]
    public async Task<IActionResult> UpdatePgDumpSettings(
        [FromBody] UpdatePgDumpSettingsRequest request
    )
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(
                    new PgDumpSettingsResponse
                    {
                        Success = false,
                        Message = "Некорректные данные запроса",
                    }
                );
            }

            // Валидируем путь перед сохранением
            var validationInfo = await pgDumpSettingsService.ValidatePgDumpPathAsync(
                request.PgDumpPath
            );
            if (!validationInfo.FileExists)
            {
                return BadRequest(
                    new PgDumpSettingsResponse
                    {
                        Success = false,
                        Message =
                            $"pg_dump не найден по указанному пути: {request.PgDumpPath}. {validationInfo.Message}",
                        ValidationInfo = validationInfo,
                    }
                );
            }

            var updatedSettings = await pgDumpSettingsService.UpdateSettingsAsync(request);

            return Ok(
                new PgDumpSettingsResponse
                {
                    Success = true,
                    Message = "Настройки pg_dump обновлены успешно",
                    Settings = updatedSettings,
                    ValidationInfo = validationInfo,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении настроек pg_dump");
            return StatusCode(
                500,
                new PgDumpSettingsResponse
                {
                    Success = false,
                    Message = "Внутренняя ошибка сервера при обновлении настроек pg_dump",
                }
            );
        }
    }

    /// <summary>
    /// Валидирует путь к pg_dump
    /// </summary>
    /// <param name="pgDumpPath">Путь к pg_dump для валидации</param>
    /// <returns>Результат валидации</returns>
    [HttpPost("pg-dump/validate")]
    public async Task<IActionResult> ValidatePgDumpPath([FromQuery] string pgDumpPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pgDumpPath))
            {
                return BadRequest(
                    new PgDumpSettingsResponse
                    {
                        Success = false,
                        Message = "Путь к pg_dump не может быть пустым",
                    }
                );
            }

            var validationInfo = await pgDumpSettingsService.ValidatePgDumpPathAsync(pgDumpPath);

            return Ok(
                new PgDumpSettingsResponse
                {
                    Success = validationInfo.FileExists,
                    Message = validationInfo.Message,
                    ValidationInfo = validationInfo,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при валидации пути pg_dump: {PgDumpPath}", pgDumpPath);
            return StatusCode(
                500,
                new PgDumpSettingsResponse
                {
                    Success = false,
                    Message = "Внутренняя ошибка сервера при валидации пути pg_dump",
                }
            );
        }
    }

    /// <summary>
    /// Получает историю настроек pg_dump
    /// </summary>
    /// <returns>История настроек pg_dump</returns>
    [HttpGet("pg-dump/history")]
    public async Task<IActionResult> GetPgDumpSettingsHistory()
    {
        try
        {
            var history = await pgDumpSettingsService.GetSettingsHistoryAsync();

            return Ok(
                new
                {
                    success = true,
                    message = "История настроек pg_dump получена успешно",
                    settings = history,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении истории настроек pg_dump");
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Внутренняя ошибка сервера при получении истории настроек pg_dump",
                }
            );
        }
    }

    /// <summary>
    /// Проверяет, настроены ли настройки pg_dump
    /// </summary>
    /// <returns>Статус конфигурации pg_dump</returns>
    [HttpGet("pg-dump/configured")]
    public async Task<IActionResult> IsPgDumpConfigured()
    {
        try
        {
            var isConfigured = await pgDumpSettingsService.IsConfiguredAsync();

            return Ok(
                new
                {
                    success = true,
                    message = isConfigured
                        ? "pg_dump настроен и готов к использованию"
                        : "pg_dump не настроен",
                    configured = isConfigured,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при проверке конфигурации pg_dump");
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Внутренняя ошибка сервера при проверке конфигурации pg_dump",
                    configured = false,
                }
            );
        }
    }
}
