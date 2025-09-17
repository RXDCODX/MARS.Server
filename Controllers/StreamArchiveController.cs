using MARS.Server.DataBaseContext;
using MARS.Server.Services.StreamAcrhive.Entitys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreamArchiveController(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<StreamArchiveController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить все конфигурации архивирования
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StreamArchiveConfig>>> GetConfigurations()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var configs = await dbContext.StreamArchiveConfigs.AsNoTracking().ToListAsync();
            return Ok(configs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении конфигураций архивирования");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить конфигурацию по ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<StreamArchiveConfig>> GetConfiguration(Guid id)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var config = await dbContext
                .StreamArchiveConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            return config == null ? NotFound($"Конфигурация с ID {id} не найдена") : Ok(config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении конфигурации {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Создать новую конфигурацию архивирования
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StreamArchiveConfig>> CreateConfiguration(
        [FromBody] StreamArchiveConfig config
    )
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Проверяем, что папка существует
            if (!Directory.Exists(config.FolderPath))
            {
                return BadRequest($"Папка {config.FolderPath} не существует");
            }

            config.Id = Guid.NewGuid();
            dbContext.StreamArchiveConfigs.Add(config);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Создана новая конфигурация архивирования {Id}", config.Id);
            return CreatedAtAction(nameof(GetConfiguration), new { id = config.Id }, config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании конфигурации архивирования");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Обновить конфигурацию архивирования
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateConfiguration(
        Guid id,
        [FromBody] StreamArchiveConfig config
    )
    {
        try
        {
            if (id != config.Id)
            {
                return BadRequest("ID в URL не совпадает с ID в теле запроса");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var existingConfig = await dbContext.StreamArchiveConfigs.FindAsync(id);
            if (existingConfig == null)
            {
                return NotFound($"Конфигурация с ID {id} не найдена");
            }

            // Проверяем, что папка существует
            if (!Directory.Exists(config.FolderPath))
            {
                return BadRequest($"Папка {config.FolderPath} не существует");
            }

            // Обновляем поля
            existingConfig.TelegramChannelId = config.TelegramChannelId;
            existingConfig.FileNameFormat = config.FileNameFormat;
            existingConfig.CheckSpan = config.CheckSpan;
            existingConfig.FolderPath = config.FolderPath;
            existingConfig.IsConvertFile = config.IsConvertFile;
            existingConfig.FileConvertType = config.FileConvertType;

            await dbContext.SaveChangesAsync();

            logger.LogInformation("Обновлена конфигурация архивирования {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении конфигурации {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Удалить конфигурацию архивирования
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteConfiguration(Guid id)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var config = await dbContext.StreamArchiveConfigs.FindAsync(id);
            if (config == null)
            {
                return NotFound($"Конфигурация с ID {id} не найдена");
            }

            dbContext.StreamArchiveConfigs.Remove(config);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Удалена конфигурация архивирования {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении конфигурации {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Проверить доступность папки
    /// </summary>
    [HttpPost("validate-folder")]
    public IActionResult ValidateFolder([FromBody] ValidateFolderRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FolderPath))
            {
                return BadRequest("Путь к папке не может быть пустым");
            }

            var exists = Directory.Exists(request.FolderPath);
            var accessible = false;
            var files = new List<string>();

            if (exists)
            {
                try
                {
                    accessible = true;
                    files = Directory
                        .GetFiles(request.FolderPath)
                        .Where(f => IsVideoFile(f))
                        .Select(Path.GetFileName)
                        .Take(10) // Показываем только первые 10 файлов
                        .ToList()!;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Папка {FolderPath} существует, но недоступна для чтения",
                        request.FolderPath
                    );
                }
            }

            return Ok(
                new ValidateFolderResponse
                {
                    Exists = exists,
                    Accessible = accessible,
                    VideoFilesCount = files.Count,
                    SampleFiles = files,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при проверке папки {FolderPath}", request.FolderPath);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить статистику по файлам для конфигурации
    /// </summary>
    [HttpGet("{configId}/files")]
    public async Task<ActionResult<object>> GetFilesStatistics(Guid configId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var files = await dbContext
                .StreamArchiveFiles.AsNoTracking()
                .Where(f => f.ConfigId == configId)
                .Include(f => f.Chunks)
                .OrderByDescending(f => f.DiscoveredAt)
                .ToListAsync();

            var statistics = new
            {
                TotalFiles = files.Count,
                CompletedFiles = files.Count(f => f.Status == StreamArchiveFileStatus.Completed),
                FailedFiles = files.Count(f => f.Status == StreamArchiveFileStatus.Failed),
                ProcessingFiles = files.Count(f => f.Status == StreamArchiveFileStatus.Processing),
                SkippedFiles = files.Count(f => f.Status == StreamArchiveFileStatus.Skipped),
                TotalSize = files.Sum(f => f.OriginalFileSize),
                TotalChunks = files.Sum(f => f.Chunks.Count),
                Files = files
                    .Select(f => new
                    {
                        f.Id,
                        f.OriginalFileName,
                        f.ProcessedFileName,
                        f.OriginalFileSize,
                        f.Status,
                        f.DiscoveredAt,
                        f.ProcessingStartedAt,
                        f.ProcessingCompletedAt,
                        f.ChunksCount,
                        f.TelegramMessageId,
                        f.ErrorMessage,
                        Chunks = f
                            .Chunks.Select(c => new
                            {
                                c.Id,
                                c.ChunkNumber,
                                c.TotalChunks,
                                c.ChunkSize,
                                c.Status,
                                c.UploadedAt,
                                c.TelegramMessageId,
                                c.ErrorMessage,
                            })
                            .ToList(),
                    })
                    .ToList(),
            };

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении статистики файлов для конфигурации {ConfigId}",
                configId
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить общую статистику по всем конфигурациям
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<object>> GetOverallStatistics()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var files = await dbContext
                .StreamArchiveFiles.AsNoTracking()
                .Include(f => f.Config)
                .Include(f => f.Chunks)
                .ToListAsync();

            var statistics = new
            {
                TotalConfigurations = await dbContext.StreamArchiveConfigs.CountAsync(),
                TotalFiles = files.Count,
                CompletedFiles = files.Count(f => f.Status == StreamArchiveFileStatus.Completed),
                FailedFiles = files.Count(f => f.Status == StreamArchiveFileStatus.Failed),
                ProcessingFiles = files.Count(f => f.Status == StreamArchiveFileStatus.Processing),
                SkippedFiles = files.Count(f => f.Status == StreamArchiveFileStatus.Skipped),
                TotalSize = files.Sum(f => f.OriginalFileSize),
                TotalChunks = files.Sum(f => f.Chunks.Count),
                CompletedChunks = files
                    .SelectMany(f => f.Chunks)
                    .Count(c => c.Status == StreamArchiveChunkStatus.Uploaded),
                FailedChunks = files
                    .SelectMany(f => f.Chunks)
                    .Count(c => c.Status == StreamArchiveChunkStatus.Failed),
                ByConfiguration = files
                    .GroupBy(f => f.ConfigId)
                    .Select(g => new
                    {
                        ConfigId = g.Key,
                        ConfigName = g.First().Config.FolderPath,
                        FilesCount = g.Count(),
                        CompletedFiles = g.Count(f =>
                            f.Status == StreamArchiveFileStatus.Completed
                        ),
                        TotalSize = g.Sum(f => f.OriginalFileSize),
                        ChunksCount = g.Sum(f => f.Chunks.Count),
                    })
                    .ToList(),
            };

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении общей статистики");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    private static bool IsVideoFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var videoExtensions = new[]
        {
            ".mp4",
            ".avi",
            ".mkv",
            ".mov",
            ".wmv",
            ".flv",
            ".webm",
            ".m4v",
            ".3gp",
        };
        return videoExtensions.Contains(extension);
    }
}

public class ValidateFolderRequest
{
    public string FolderPath { get; set; } = null!;
}

public class ValidateFolderResponse
{
    public bool Exists { get; set; }
    public bool Accessible { get; set; }
    public int VideoFilesCount { get; set; }
    public List<string> SampleFiles { get; set; } = [];
}
