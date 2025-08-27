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
    ILogger<DatabaseBackupController> logger
) : ControllerBase
{
    private readonly IDatabaseBackupService _backupService = backupService;
    private readonly ILogger<DatabaseBackupController> _logger = logger;

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

            var downloadUrl = await _backupService.CreateBackupAsync(databaseName);
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
            _logger.LogError(
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

            var backupStream = await _backupService.GetBackupFileAsync(fileName);

            return File(backupStream, "application/sql", fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { success = false, message = "Файл резервной копии не найден" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при скачивании резервной копии {FileName}", fileName);
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
            var backupFileNames = await _backupService.GetAvailableBackupsAsync();
            var backupInfo = new List<BackupFileInfo>();

            foreach (var fileName in backupFileNames)
            {
                var fileInfo = await _backupService.GetBackupFileInfoAsync(fileName);
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
            _logger.LogError(ex, "Ошибка при получении списка резервных копий");
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

            var deletedCount = await _backupService.CleanupOldBackupsAsync(keepCount);

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
            _logger.LogError(ex, "Ошибка при очистке старых резервных копий");
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
            var backupFileNames = await _backupService.GetAvailableBackupsAsync();
            var backupFiles = backupFileNames.ToList();

            var totalSize = 0L;
            var oldestBackup = (DateTime?)null;
            var newestBackup = (DateTime?)null;

            if (backupFiles.Count > 0)
            {
                foreach (var fileName in backupFiles)
                {
                    var fileInfo = await _backupService.GetBackupFileInfoAsync(fileName);
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
            _logger.LogError(ex, "Ошибка при получении статуса резервного копирования");
            return StatusCode(500, new BackupStatusResponse { Success = false });
        }
    }
}
