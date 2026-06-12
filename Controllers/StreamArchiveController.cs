using System.Collections.Generic;
using System.IO;
using System.Linq;
using MARS.Server.Services.StreamAcrhive_UNUSED.Entitys;
using Microsoft.Extensions.Logging;

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
    public async Task<ActionResult<OperationResult<List<StreamArchiveConfig>>>> GetConfigurations()
    {
        ActionResult<OperationResult<List<StreamArchiveConfig>>> result = null!;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var configs = await dbContext.StreamArchiveConfigs.AsNoTracking().ToListAsync();
            result = Ok(
                OperationResult<List<StreamArchiveConfig>>.Ok(
                    "Получены конфигурации архивирования",
                    configs
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении конфигураций архивирования");
            result = Ok(
                OperationResult<List<StreamArchiveConfig>>.Bad(
                    "Ошибка при получении конфигураций",
                    []
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить конфигурацию по ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<OperationResult<StreamArchiveConfig?>>> GetConfiguration(Guid id)
    {
        ActionResult<OperationResult<StreamArchiveConfig?>> result = null!;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var config = await dbContext
                .StreamArchiveConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (config != null)
            {
                result = Ok(
                    OperationResult<StreamArchiveConfig?>.Ok("Конфигурация найдена", config)
                );
            }
            else
            {
                result = Ok(
                    OperationResult<StreamArchiveConfig?>.Bad(
                        $"Конфигурация с ID {id} не найдена",
                        null
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении конфигурации {Id}", id);
            result = Ok(
                OperationResult<StreamArchiveConfig?>.Bad("Ошибка при получении конфигурации", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Создать новую конфигурацию архивирования
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OperationResult<StreamArchiveConfig?>>> CreateConfiguration(
        [FromBody] StreamArchiveConfig config
    )
    {
        ActionResult<OperationResult<StreamArchiveConfig?>> result = null!;

        try
        {
            if (!ModelState.IsValid)
            {
                result = Ok(
                    OperationResult<StreamArchiveConfig?>.Bad("Некорректные данные модели", null)
                );
            }
            else if (!Directory.Exists(config.FolderPath))
            {
                result = Ok(
                    OperationResult<StreamArchiveConfig?>.Bad(
                        $"Папка {config.FolderPath} не существует",
                        null
                    )
                );
            }
            else
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();

                config.Id = Guid.NewGuid();
                dbContext.StreamArchiveConfigs.Add(config);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Создана новая конфигурация архивирования {Id}", config.Id);
                result = Ok(
                    OperationResult<StreamArchiveConfig?>.Ok("Конфигурация успешно создана", config)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании конфигурации архивирования");
            result = Ok(
                OperationResult<StreamArchiveConfig?>.Bad("Ошибка при создании конфигурации", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Обновить конфигурацию архивирования
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<OperationResult>> UpdateConfiguration(
        Guid id,
        [FromBody] StreamArchiveConfig config
    )
    {
        ActionResult<OperationResult> result = null!;

        try
        {
            if (id != config.Id)
            {
                result = Ok(OperationResult.Bad("ID в URL не совпадает с ID в теле запроса"));
            }
            else if (!ModelState.IsValid)
            {
                result = Ok(OperationResult.Bad("Некорректные данные модели"));
            }
            else if (!Directory.Exists(config.FolderPath))
            {
                result = Ok(OperationResult.Bad($"Папка {config.FolderPath} не существует"));
            }
            else
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();

                var existingConfig = await dbContext.StreamArchiveConfigs.FindAsync(id);
                if (existingConfig == null)
                {
                    result = Ok(OperationResult.Bad($"Конфигурация с ID {id} не найдена"));
                }
                else
                {
                    // Обновляем поля
                    existingConfig.TelegramChannelId = config.TelegramChannelId;
                    existingConfig.FileNameFormat = config.FileNameFormat;
                    existingConfig.CheckSpan = config.CheckSpan;
                    existingConfig.FolderPath = config.FolderPath;
                    existingConfig.IsConvertFile = config.IsConvertFile;
                    existingConfig.FileConvertType = config.FileConvertType;

                    await dbContext.SaveChangesAsync();

                    logger.LogInformation("Обновлена конфигурация архивирования {Id}", id);
                    result = Ok(OperationResult.Ok("Конфигурация успешно обновлена"));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении конфигурации {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при обновлении конфигурации"));
        }

        return result;
    }

    /// <summary>
    /// Удалить конфигурацию архивирования
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<OperationResult>> DeleteConfiguration(Guid id)
    {
        ActionResult<OperationResult> result = null!;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var config = await dbContext.StreamArchiveConfigs.FindAsync(id);
            if (config == null)
            {
                result = Ok(OperationResult.Bad($"Конфигурация с ID {id} не найдена"));
            }
            else
            {
                dbContext.StreamArchiveConfigs.Remove(config);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Удалена конфигурация архивирования {Id}", id);
                result = Ok(OperationResult.Ok("Конфигурация успешно удалена"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении конфигурации {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при удалении конфигурации"));
        }

        return result;
    }

    /// <summary>
    /// Проверить доступность папки
    /// </summary>
    [HttpPost("validate-folder")]
    public ActionResult<OperationResult<ValidateFolderResponse>> ValidateFolder(
        [FromBody] ValidateFolderRequest request
    )
    {
        ActionResult<OperationResult<ValidateFolderResponse>> result = null!;

        try
        {
            if (string.IsNullOrWhiteSpace(request.FolderPath))
            {
                result = Ok(
                    OperationResult<ValidateFolderResponse>.Bad(
                        "Путь к папке не может быть пустым",
                        new ValidateFolderResponse()
                    )
                );
            }
            else
            {
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

                var response = new ValidateFolderResponse
                {
                    Exists = exists,
                    Accessible = accessible,
                    VideoFilesCount = files.Count,
                    SampleFiles = files,
                };

                result = Ok(
                    OperationResult<ValidateFolderResponse>.Ok("Папка проверена", response)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при проверке папки {FolderPath}", request.FolderPath);
            result = Ok(
                OperationResult<ValidateFolderResponse>.Bad(
                    "Ошибка при проверке папки",
                    new ValidateFolderResponse()
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить статистику по файлам для конфигурации
    /// </summary>
    [HttpGet("{configId}/files")]
    public async Task<ActionResult<OperationResult<object>>> GetFilesStatistics(Guid configId)
    {
        ActionResult<OperationResult<object>> result = null!;

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

            result = Ok(OperationResult<object>.Ok("Получена статистика файлов", statistics));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении статистики файлов для конфигурации {ConfigId}",
                configId
            );
            result = Ok(
                OperationResult<object>.Bad("Ошибка при получении статистики файлов", new { })
            );
        }

        return result;
    }

    /// <summary>
    /// Получить общую статистику по всем конфигурациям
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<OperationResult<object>>> GetOverallStatistics()
    {
        ActionResult<OperationResult<object>> result = null!;

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

            result = Ok(OperationResult<object>.Ok("Получена общая статистика", statistics));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении общей статистики");
            result = Ok(
                OperationResult<object>.Bad("Ошибка при получении общей статистики", new { })
            );
        }

        return result;
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
