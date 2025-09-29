using MARS.Server.Services.StreamAcrhive_UNUSED.Entitys;
using MARS.Server.Services.StreamAcrhive_UNUSED.Interfaces;
using TL;
using InputFile = TL.InputFile;

namespace MARS.Server.Services.StreamAcrhive_UNUSED;

public class StreamArchiveService(
    WTelegramClient telegramClient,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<StreamArchiveService> logger,
    IFFmpegService ffmpegService
) : BackgroundService, IStreamArchiveService
{
    private readonly Dictionary<Guid, Task> _activeTasks = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Запуск сервиса архивирования потоков");

        // Загружаем конфигурации при запуске
        await LoadConfigurationsAsync();

        // Подписываемся на изменения в базе данных
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Проверяем изменения в конфигурациях каждые 30 секунд
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                await LoadConfigurationsAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в основном цикле сервиса архивирования");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        // Останавливаем все задачи
        foreach (var task in _activeTasks.Values)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при остановке задачи архивирования");
            }
        }
        _activeTasks.Clear();

        logger.LogInformation("Сервис архивирования потоков остановлен");
    }

    private async Task LoadConfigurationsAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var configs = await dbContext.StreamArchiveConfigs.AsNoTracking().ToListAsync();

            // Удаляем задачи для конфигураций, которых больше нет
            var configIds = configs.Select(c => c.Id).ToHashSet();
            var tasksToRemove = _activeTasks.Keys.Where(id => !configIds.Contains(id)).ToList();

            foreach (var configId in tasksToRemove)
            {
                if (_activeTasks.TryGetValue(configId, out var task))
                {
                    _activeTasks.Remove(configId);
                    logger.LogInformation(
                        "Остановлена задача для конфигурации {ConfigId}",
                        configId
                    );
                }
            }

            // Создаем или обновляем задачи для активных конфигураций
            foreach (var config in configs)
            {
                if (!_activeTasks.ContainsKey(config.Id))
                {
                    var task = StartArchiveTaskForConfig(config);
                    _activeTasks[config.Id] = task;
                    logger.LogInformation(
                        "Запущена задача для конфигурации {ConfigId} с интервалом {Interval}",
                        config.Id,
                        config.CheckSpan
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при загрузке конфигураций архивирования");
        }
    }

    private async Task StartArchiveTaskForConfig(StreamArchiveConfig config)
    {
        try
        {
            var periodicTimer = new PeriodicTimer(config.CheckSpan);

            while (
                !_cancellationTokenSource.Token.IsCancellationRequested
                && await periodicTimer.WaitForNextTickAsync(_cancellationTokenSource.Token)
            )
            {
                await ProcessArchiveForConfig(config);
            }

            periodicTimer.Dispose();
        }
        catch (OperationCanceledException)
        {
            // Нормальная остановка
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка в задаче архивирования для конфигурации {ConfigId}",
                config.Id
            );
        }
    }

    private async Task ProcessArchiveForConfig(StreamArchiveConfig config)
    {
        try
        {
            logger.LogDebug("Обработка архивирования для конфигурации {ConfigId}", config.Id);

            if (!Directory.Exists(config.FolderPath))
            {
                logger.LogWarning(
                    "Папка {FolderPath} не существует для конфигурации {ConfigId}",
                    config.FolderPath,
                    config.Id
                );
                return;
            }

            var files = Directory
                .GetFiles(config.FolderPath)
                .Where(f => IsVideoFile(f))
                .OrderBy(f => new FileInfo(f).CreationTime)
                .ToList();

            foreach (var filePath in files)
            {
                try
                {
                    await ProcessFileAsync(filePath, config);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ошибка при обработке файла {FilePath}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обработке архивирования для конфигурации {ConfigId}",
                config.Id
            );
        }
    }

    private async Task ProcessFileAsync(string filePath, StreamArchiveConfig config)
    {
        var fileInfo = new FileInfo(filePath);
        var fileName = GenerateFileName(fileInfo.Name, config.FileNameFormat);

        logger.LogInformation(
            "Обработка файла {FileName} размером {Size} байт",
            fileName,
            fileInfo.Length
        );

        StreamArchiveFile archiveFile;

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            // Проверяем, не обрабатывался ли уже этот файл
            var existingFile = await dbContext
                .StreamArchiveFiles.AsNoTracking()
                .FirstOrDefaultAsync(f =>
                    f.ConfigId == config.Id && f.OriginalFilePath == filePath
                );

            if (existingFile != null)
            {
                logger.LogInformation("Файл {FilePath} уже был обработан, пропускаем", filePath);
                return;
            }

            // Создаем запись о файле в базе данных
            archiveFile = new StreamArchiveFile
            {
                Id = Guid.NewGuid(),
                ConfigId = config.Id,
                OriginalFileName = fileInfo.Name,
                ProcessedFileName = fileName,
                OriginalFilePath = filePath,
                OriginalFileSize = fileInfo.Length,
                DiscoveredAt = DateTime.UtcNow,
                ProcessingStartedAt = DateTime.UtcNow,
                Status = StreamArchiveFileStatus.Processing,
                ChunksCount =
                    fileInfo.Length > 2L * 1024 * 1024 * 1024
                        ? (int)Math.Ceiling((double)fileInfo.Length / (2L * 1024 * 1024 * 1024))
                        : 1,
            };

            dbContext.StreamArchiveFiles.Add(archiveFile);
            await dbContext.SaveChangesAsync();
        }

        try
        {
            // Если файл меньше 2ГБ, загружаем как есть
            if (fileInfo.Length <= 2L * 1024 * 1024 * 1024)
            {
                await UploadSingleFileAsync(filePath, fileName, config, archiveFile);
            }
            else
            {
                // Разбиваем файл на части с помощью FFmpeg
                await SplitAndUploadFileAsync(filePath, fileName, config, archiveFile);
            }

            // Обновляем статус файла
            await UpdateFileStatusAsync(
                archiveFile.Id,
                StreamArchiveFileStatus.Completed,
                processingCompletedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке файла {FilePath}", filePath);
            await UpdateFileStatusAsync(
                archiveFile.Id,
                StreamArchiveFileStatus.Failed,
                errorMessage: ex.Message
            );
            throw;
        }

        // Удаляем обработанный файл
        try
        {
            File.Delete(filePath);
            logger.LogInformation("Файл {FilePath} удален после обработки", filePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось удалить файл {FilePath}", filePath);
        }
    }

    private async Task SplitAndUploadFileAsync(
        string filePath,
        string baseFileName,
        StreamArchiveConfig config,
        StreamArchiveFile archiveFile
    )
    {
        const long maxChunkSize = 2L * 1024 * 1024 * 1024; // 2ГБ
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"stream_archive_{archiveFile.Id}");

        try
        {
            Directory.CreateDirectory(tempDirectory);

            logger.LogInformation(
                "Разбивка файла {FileName} на части с помощью FFmpeg",
                baseFileName
            );

            // Используем FFmpeg для разбивки файла
            var chunkPaths = await ffmpegService.SplitVideoFileAsync(
                filePath,
                tempDirectory,
                maxChunkSize
            );

            var totalChunks = chunkPaths.Count;

            // Обновляем количество частей в базе данных
            await UpdateFileChunksCountAsync(archiveFile.Id, totalChunks);

            for (var i = 0; i < chunkPaths.Count; i++)
            {
                var chunkPath = chunkPaths[i];
                var chunkNumber = i + 1;
                var chunkFileName =
                    $"{Path.GetFileNameWithoutExtension(baseFileName)}_part_{chunkNumber}_of_{totalChunks}{Path.GetExtension(baseFileName)}";

                try
                {
                    var chunkInfo = new FileInfo(chunkPath);

                    // Создаем запись о части файла в базе данных
                    var fileChunk = await CreateFileChunkAsync(
                        archiveFile.Id,
                        chunkNumber,
                        totalChunks,
                        chunkFileName,
                        chunkInfo.Length,
                        chunkInfo.Length * i
                    );

                    await UploadChunkAsync(chunkPath, chunkFileName, config, fileChunk);
                }
                finally
                {
                    // Удаляем временный файл части
                    try
                    {
                        if (File.Exists(chunkPath))
                        {
                            File.Delete(chunkPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Не удалось удалить временный файл {ChunkPath}",
                            chunkPath
                        );
                    }
                }
            }
        }
        finally
        {
            // Удаляем временную директорию
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Не удалось удалить временную директорию {TempDirectory}",
                    tempDirectory
                );
            }
        }
    }

    private static string GenerateFileName(string originalFileName, string format)
    {
        var now = DateTime.Now;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
        var extension = Path.GetExtension(originalFileName);

        return format
                .Replace("{date}", now.ToString("yyyy-MM-dd"))
                .Replace("{time}", now.ToString("HH-mm-ss"))
                .Replace("{datetime}", now.ToString("yyyy-MM-dd_HH-mm-ss"))
                .Replace("{original}", fileNameWithoutExtension)
                .Replace("{timestamp}", DateTimeOffset.Now.ToUnixTimeSeconds().ToString())
            + extension;
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

    private static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".flv" => "video/x-flv",
            ".webm" => "video/webm",
            ".m4v" => "video/x-m4v",
            ".3gp" => "video/3gpp",
            _ => "application/octet-stream",
        };
    }

    public new async Task StartAsync(CancellationToken cancellationToken)
    {
        await ExecuteAsync(cancellationToken);
    }

    public new Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }

    private async Task UploadSingleFileAsync(
        string filePath,
        string fileName,
        StreamArchiveConfig config,
        StreamArchiveFile archiveFile
    )
    {
        try
        {
            logger.LogInformation(
                "Загрузка файла {FileName} в Telegram канал {ChannelId}",
                fileName,
                config.TelegramChannelId
            );

            await using var fileStream = File.OpenRead(filePath);

            // Получаем информацию о канале
            var chats = await telegramClient.Messages_GetAllChats();
            if (!chats.chats.TryGetValue((long)config.TelegramChannelId, out var channel))
            {
                logger.LogError("Канал {ChannelId} не найден", config.TelegramChannelId);
                throw new InvalidOperationException($"Канал {config.TelegramChannelId} не найден");
            }

            // Загружаем файл
            var uploadedFile = await telegramClient.UploadFileAsync(fileStream, fileName);

            // Определяем тип медиа
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var mimeType = GetMimeType(extension);

            // Отправляем файл в канал
            long messageId;
            if (IsVideoFile(filePath))
            {
                messageId = await SendVideoAsync(
                    channel,
                    (InputFile)uploadedFile,
                    fileName,
                    mimeType
                );
            }
            else
            {
                messageId = await SendDocumentAsync(
                    channel,
                    (InputFile)uploadedFile,
                    fileName,
                    mimeType
                );
            }

            // Обновляем информацию о файле в базе данных
            await UpdateFileTelegramInfoAsync(archiveFile.Id, messageId);

            logger.LogInformation(
                "Файл {FileName} успешно загружен в канал {ChannelId} с ID сообщения {MessageId}",
                fileName,
                config.TelegramChannelId,
                messageId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при загрузке файла {FileName} в канал {ChannelId}",
                fileName,
                config.TelegramChannelId
            );
            throw;
        }
    }

    private async Task UploadChunkAsync(
        string chunkPath,
        string chunkFileName,
        StreamArchiveConfig config,
        StreamArchiveFileChunk fileChunk
    )
    {
        try
        {
            logger.LogInformation(
                "Загрузка части {ChunkNumber} из {TotalChunks} файла {FileName}",
                fileChunk.ChunkNumber,
                fileChunk.TotalChunks,
                chunkFileName
            );

            await using var fileStream = File.OpenRead(chunkPath);

            // Получаем информацию о канале
            var chats = await telegramClient.Messages_GetAllChats();
            if (!chats.chats.TryGetValue((long)config.TelegramChannelId, out var channel))
            {
                logger.LogError("Канал {ChannelId} не найден", config.TelegramChannelId);
                throw new InvalidOperationException($"Канал {config.TelegramChannelId} не найден");
            }

            // Загружаем файл
            var uploadedFile = await telegramClient.UploadFileAsync(fileStream, chunkFileName);

            // Определяем тип медиа
            var extension = Path.GetExtension(chunkFileName).ToLowerInvariant();
            var mimeType = GetMimeType(extension);

            // Отправляем файл в канал
            long messageId;
            if (IsVideoFile(chunkPath))
            {
                messageId = await SendVideoAsync(
                    channel,
                    (InputFile)uploadedFile,
                    chunkFileName,
                    mimeType
                );
            }
            else
            {
                messageId = await SendDocumentAsync(
                    channel,
                    (InputFile)uploadedFile,
                    chunkFileName,
                    mimeType
                );
            }

            // Обновляем информацию о части файла в базе данных
            await UpdateChunkTelegramInfoAsync(fileChunk.Id, messageId);

            logger.LogInformation(
                "Часть {ChunkNumber} файла {FileName} успешно загружена с ID сообщения {MessageId}",
                fileChunk.ChunkNumber,
                chunkFileName,
                messageId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при загрузке части {ChunkNumber} файла {FileName}",
                fileChunk.ChunkNumber,
                chunkFileName
            );

            // Обновляем статус части как неудачной
            await UpdateChunkStatusAsync(fileChunk.Id, StreamArchiveChunkStatus.Failed, ex.Message);
            throw;
        }
    }

    private async Task<StreamArchiveFileChunk> CreateFileChunkAsync(
        Guid fileId,
        int chunkNumber,
        int totalChunks,
        string chunkFileName,
        long chunkSize,
        long offset
    )
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var fileChunk = new StreamArchiveFileChunk
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            ChunkNumber = chunkNumber,
            TotalChunks = totalChunks,
            ChunkFileName = chunkFileName,
            ChunkSize = chunkSize,
            OffsetInOriginalFile = offset,
            Status = StreamArchiveChunkStatus.Created,
        };

        dbContext.StreamArchiveFileChunks.Add(fileChunk);
        await dbContext.SaveChangesAsync();

        return fileChunk;
    }

    private async Task UpdateFileStatusAsync(
        Guid fileId,
        StreamArchiveFileStatus status,
        DateTime? processingCompletedAt = null,
        string? errorMessage = null
    )
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var file = await dbContext.StreamArchiveFiles.FindAsync(fileId);
        if (file != null)
        {
            file.Status = status;
            if (processingCompletedAt.HasValue)
            {
                file.ProcessingCompletedAt = processingCompletedAt;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                file.ErrorMessage = errorMessage;
            }

            await dbContext.SaveChangesAsync();
        }
    }

    private async Task UpdateFileChunksCountAsync(Guid fileId, int chunksCount)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var file = await dbContext.StreamArchiveFiles.FindAsync(fileId);
        if (file != null)
        {
            file.ChunksCount = chunksCount;
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task UpdateFileTelegramInfoAsync(Guid fileId, long messageId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var file = await dbContext.StreamArchiveFiles.FindAsync(fileId);
        if (file != null)
        {
            file.TelegramMessageId = messageId;
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task UpdateChunkStatusAsync(
        Guid chunkId,
        StreamArchiveChunkStatus status,
        string? errorMessage = null
    )
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var chunk = await dbContext.StreamArchiveFileChunks.FindAsync(chunkId);
        if (chunk != null)
        {
            chunk.Status = status;
            if (!string.IsNullOrEmpty(errorMessage))
            {
                chunk.ErrorMessage = errorMessage;
            }

            if (status == StreamArchiveChunkStatus.Uploaded)
            {
                chunk.UploadedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync();
        }
    }

    private async Task UpdateChunkTelegramInfoAsync(Guid chunkId, long messageId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var chunk = await dbContext.StreamArchiveFileChunks.FindAsync(chunkId);
        if (chunk != null)
        {
            chunk.TelegramMessageId = messageId;
            chunk.Status = StreamArchiveChunkStatus.Uploaded;
            chunk.UploadedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task<long> SendVideoAsync(
        InputPeer channel,
        InputFile uploadedFile,
        string fileName,
        string mimeType
    )
    {
        var attributes = new List<DocumentAttribute>
        {
            new DocumentAttributeFilename { file_name = fileName },
        };

        await telegramClient.Messages_SendMedia(
            channel,
            new InputMediaUploadedDocument
            {
                file = uploadedFile,
                mime_type = mimeType,
                attributes = [.. attributes],
            },
            $"📹 {fileName}",
            Random.Shared.NextInt64()
        );

        // Возвращаем случайный ID, так как WTelegram не возвращает ID сообщения напрямую
        return Random.Shared.NextInt64();
    }

    private async Task<long> SendDocumentAsync(
        InputPeer channel,
        InputFile uploadedFile,
        string fileName,
        string mimeType
    )
    {
        var attributes = new List<DocumentAttribute>
        {
            new DocumentAttributeFilename { file_name = fileName },
        };

        await telegramClient.Messages_SendMedia(
            channel,
            new InputMediaUploadedDocument
            {
                file = uploadedFile,
                mime_type = mimeType,
                attributes = [.. attributes],
            },
            $"📄 {fileName}",
            Random.Shared.NextInt64()
        );

        // Возвращаем случайный ID, так как WTelegram не возвращает ID сообщения напрямую
        return Random.Shared.NextInt64();
    }

    public new void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }
}
