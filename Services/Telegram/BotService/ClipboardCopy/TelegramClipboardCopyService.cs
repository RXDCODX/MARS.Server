using System.Collections.Concurrent;
using MARS.Server.Services.MemoryStorageService;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.Telegram.BotService.ClipboardCopy;

public class TelegramClipboardCopyService(
    ILogger<TelegramClipboardCopyService> logger,
    IHostApplicationLifetime applicationLifetime,
    IConfiguration configuration
) : ITelegramusService
{
    private const int MediaGroupDebounceMs = 1200;
    private const string ClipboardPagePath = "/telegram-copy";
    private const int DefaultRequestTtlMinutes = 30;

    private static readonly string[] TriggerWords = ["copy", "копи", "копипаста", "копировать"];
    private readonly ConcurrentDictionary<string, MediaGroupBuffer> _mediaGroupBuffers = new();
    private readonly ConcurrentDictionary<string, ClipboardRequestFiles> _clipboardRequests = new();
    private readonly TimeSpan _requestTtl = ResolveRequestTtl(configuration);

    private CancellationToken StoppingToken => applicationLifetime.ApplicationStopping;

    public Task HandMessage(ITelegramBotClient client, Update update)
    {
        var result = Task.CompletedTask;

        if (update.Type == UpdateType.Message && update.Message is { } message)
        {
            result = HandleMessageAsync(client, message);
        }

        return result;
    }

    private async Task HandleMessageAsync(ITelegramBotClient client, Message message)
    {
        if (message.Photo is not { Length: > 0 })
        {
            return;
        }

        var textWithTrigger = message.Caption ?? message.Text ?? string.Empty;
        var hasTrigger = ContainsTrigger(textWithTrigger);

        if (string.IsNullOrWhiteSpace(message.MediaGroupId))
        {
            if (hasTrigger)
            {
                await ProcessMessagesAsync(client, message.Chat.Id, [message], null);
            }
        }
        else
        {
            await HandleMediaGroupMessageAsync(client, message, hasTrigger);
        }
    }

    private async Task HandleMediaGroupMessageAsync(
        ITelegramBotClient client,
        Message message,
        bool hasTrigger
    )
    {
        var mediaGroupId = message.MediaGroupId!;
        var stateExists = _mediaGroupBuffers.TryGetValue(mediaGroupId, out var existingState);

        if (!stateExists && !hasTrigger)
        {
            return;
        }

        var state = existingState;
        if (!stateExists)
        {
            state = new MediaGroupBuffer();
            _mediaGroupBuffers[mediaGroupId] = state;
        }

        state!.Messages[message.MessageId] = message;
        state.ResetDebounce();

        var debounceCts = CancellationTokenSource.CreateLinkedTokenSource(StoppingToken);
        state.DebounceCts = debounceCts;

        try
        {
            await Task.Delay(MediaGroupDebounceMs, debounceCts.Token);

            if (Interlocked.Exchange(ref state.IsProcessed, 1) == 0)
            {
                _mediaGroupBuffers.TryRemove(mediaGroupId, out _);
                var orderedMessages = state.Messages.Values.OrderBy(m => m.MessageId).ToList();
                await ProcessMessagesAsync(client, message.Chat.Id, orderedMessages, mediaGroupId);
            }
        }
        catch (TaskCanceledException)
        {
            // Следующее сообщение в альбоме сбросило таймер.
        }
    }

    private async Task ProcessMessagesAsync(
        ITelegramBotClient client,
        long chatId,
        IReadOnlyCollection<Message> messages,
        string? mediaGroupId
    )
    {
        await CleanupExpiredRequestsAsync();

        var memoryFileNames = new List<string>();
        var fileIndex = 1;
        var requestId = BuildRequestId(mediaGroupId);

        foreach (var message in messages)
        {
            try
            {
                var fileId = message.Photo?.LastOrDefault()?.FileId;
                if (string.IsNullOrWhiteSpace(fileId))
                {
                    continue;
                }

                var telegramFile = await client.GetFile(fileId, StoppingToken);
                if (string.IsNullOrWhiteSpace(telegramFile.FilePath))
                {
                    continue;
                }

                var extension = Path.GetExtension(telegramFile.FilePath);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".jpg";
                }

                var memoryFileName = BuildMemoryFileName(requestId, fileIndex, extension);

                await using var memoryStream = new MemoryStream();
                await client.DownloadFile(telegramFile.FilePath, memoryStream, StoppingToken);

                var bytes = memoryStream.ToArray();
                await MemoryStorage.AddFileAsync(memoryFileName, bytes);

                memoryFileNames.Add(memoryFileName);
                fileIndex += 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка загрузки фото для буфера обмена");
            }
        }

        var answerText = BuildResultMessage(memoryFileNames, requestId);
        await client.SendMessage(chatId, answerText, cancellationToken: StoppingToken);
    }

    public async Task<OperationResult<string[]>> GetFileUrlsByRequestIdAsync(string requestId)
    {
        var result = OperationResult<string[]>.Bad("Запрос не найден или файлы уже недоступны", []);

        await CleanupExpiredRequestsAsync();

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            var found = _clipboardRequests.TryGetValue(requestId, out var files);
            if (found && files is not null && files.MemoryFileNames.Length > 0)
            {
                if (!IsExpired(files.CreatedAt))
                {
                    var urls = files.MemoryFileNames.Select(ToMemoryUrl).ToArray();
                    result = OperationResult<string[]>.Ok("Файлы найдены", urls);
                }
                else
                {
                    _clipboardRequests.TryRemove(requestId, out _);
                    await CleanupMemoryFilesAsync(files.MemoryFileNames);
                    result = OperationResult<string[]>.Bad("Срок действия запроса истек", []);
                }
            }
        }

        return result;
    }

    public async Task<OperationResult> MarkRequestAsCompletedAsync(string requestId)
    {
        var result = OperationResult.Bad("ID запроса не передан");

        await CleanupExpiredRequestsAsync();

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            var removed = _clipboardRequests.TryRemove(requestId, out var files);

            if (removed)
            {
                if (files is not null)
                {
                    await CleanupMemoryFilesAsync(files.MemoryFileNames);
                }
                result = OperationResult.Ok("Запрос завершен");
            }
            else
            {
                result = OperationResult.Bad("Запрос не найден");
            }
        }

        return result;
    }

    private string BuildResultMessage(IReadOnlyCollection<string> memoryFileNames, string requestId)
    {
        var result = "Не удалось скачать изображения для копирования.";

        if (memoryFileNames.Count > 0)
        {
            var requestFiles = new ClipboardRequestFiles(
                memoryFileNames.ToArray(),
                DateTimeOffset.UtcNow
            );
            _clipboardRequests[requestId] = requestFiles;

            var pageUrl = BuildClipboardPageUrl(requestId);
            result =
                $"Готово. Открой страницу и нажми кнопку \"Скопировать все\":\n{pageUrl}\n\nФайлов: {memoryFileNames.Count}.";
        }

        return result;
    }

    private async Task CleanupExpiredRequestsAsync()
    {
        var snapshot = _clipboardRequests.ToArray();

        foreach (var item in snapshot)
        {
            if (IsExpired(item.Value.CreatedAt))
            {
                var removed = _clipboardRequests.TryRemove(item.Key, out var removedFiles);
                if (removed && removedFiles is not null)
                {
                    await CleanupMemoryFilesAsync(removedFiles.MemoryFileNames);
                }
            }
        }
    }

    private static TimeSpan ResolveRequestTtl(IConfiguration configurationValue)
    {
        var configuredMinutes =
            configurationValue["AppSettings:TelegramClipboardCopy:RequestTtlMinutes"]
            ?? configurationValue["TelegramClipboardCopy:RequestTtlMinutes"];

        var parsed = int.TryParse(configuredMinutes, out var minutes);
        if (!parsed || minutes <= 0)
        {
            minutes = DefaultRequestTtlMinutes;
        }

        return TimeSpan.FromMinutes(minutes);
    }

    private async Task CleanupMemoryFilesAsync(IReadOnlyCollection<string> memoryFileNames)
    {
        foreach (var memoryFileName in memoryFileNames)
        {
            try
            {
                if (MemoryStorage.FileExists(memoryFileName))
                {
                    await MemoryStorage.DeleteFileAsync(memoryFileName);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Не удалось удалить файл {FileName} при очистке",
                    memoryFileName
                );
            }
        }
    }

    private bool IsExpired(DateTimeOffset createdAt)
    {
        var result = DateTimeOffset.UtcNow - createdAt > _requestTtl;
        return result;
    }

    private static bool ContainsTrigger(string sourceText)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(sourceText))
        {
            var normalized = sourceText.ToLowerInvariant();
            result = TriggerWords.Any(word => normalized.Contains(word, StringComparison.Ordinal));
        }

        return result;
    }

    private string BuildRequestId(string? mediaGroupId)
    {
        var result = mediaGroupId;
        if (string.IsNullOrWhiteSpace(result))
        {
            result = Guid.NewGuid().ToString("N");
        }

        return result;
    }

    private static string BuildMemoryFileName(string requestId, int fileIndex, string extension)
    {
        var result = $"telegram-copy/{requestId}/{fileIndex:D2}{extension}";

        return result;
    }

    private static string ToMemoryUrl(string memoryFileName)
    {
        var result = $"/memory/{Uri.EscapeDataString(memoryFileName)}";

        return result;
    }

    private string BuildClipboardPageUrl(string requestId)
    {
        var baseUrl = ResolveBaseUrl();
        var result = $"{baseUrl}{ClipboardPagePath}?id={Uri.EscapeDataString(requestId)}";

        return result;
    }

    private string ResolveBaseUrl()
    {
        var configuredUrl =
            configuration["AppSettings:PublicBaseUrl"]
            ?? configuration["PublicBaseUrl"]
            ?? configuration["urls"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            ?? "http://localhost:9255/";

        var firstUrl =
            configuredUrl
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(url => url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            ?? "http://localhost:9255/";

        firstUrl = firstUrl.Replace("+", "localhost").Replace("*", "localhost");

        if (firstUrl.Contains("0.0.0.0", StringComparison.Ordinal))
        {
            firstUrl = firstUrl.Replace("0.0.0.0", "localhost", StringComparison.Ordinal);
        }

        return firstUrl.TrimEnd('/');
    }

    private sealed class MediaGroupBuffer
    {
        public ConcurrentDictionary<int, Message> Messages { get; } = new();
        public CancellationTokenSource? DebounceCts { get; set; }
        public int IsProcessed;

        public void ResetDebounce()
        {
            DebounceCts?.Cancel();
            DebounceCts?.Dispose();
        }
    }

    private sealed class ClipboardRequestFiles(string[] memoryFileNames, DateTimeOffset createdAt)
    {
        public string[] MemoryFileNames { get; } = memoryFileNames;
        public DateTimeOffset CreatedAt { get; } = createdAt;
    }
}
