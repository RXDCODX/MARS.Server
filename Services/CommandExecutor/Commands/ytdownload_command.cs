using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.YouTube;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class YtdownloadCommand(
    YouTubeResolver youtubeResolver,
    ILogger<YtdownloadCommand> logger,
    ITelegramBotClient client
) : BaseCommand
{
    private readonly YoutubeClient _youtubeClient = new();

    public override string CommandName => "ytdownload";
    public override string Description => "Скачать видео из YouTube";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];

    public override CommandVisibility Visibility => CommandVisibility.All;

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "url",
                Description = "URL видео с YouTube",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "message",
                Description = "Message Id",
                Required = true,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Ошибка обработки видео";

        var hasUrl =
            parameters.TryGetValue("url", out var urlObj)
            && !string.IsNullOrWhiteSpace(urlObj?.ToString());

        if (
            hasUrl
            && parameters.TryGetValue("message", out var messageObj)
            && messageObj is Message message
        )
        {
            var url = urlObj!.ToString()!.Trim();

            try
            {
                // Получаем информацию о видео (валидация URL)
                var videoInfo = await youtubeResolver.ResolveVideoAsync(url, cancellationToken);

                if (videoInfo is not null)
                {
                    result =
                        $"✅ Видео начинает обрабатываться: {videoInfo.Title}\n⏳ Скачивание может занять время...";

                    // Запускаем скачивание в отдельном потоке без ожидания
                    _ = Task.Factory.StartNew(
                        () =>
                            DownloadAndSendVideoAsync(
                                url,
                                videoInfo.Title,
                                message,
                                CancellationToken.None
                            ),
                        cancellationToken
                    );
                }
                else
                {
                    result = "❌ Не удалось получить информацию о видео. Проверьте ссылку.";
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при обработке URL YouTube");
                result = $"❌ Ошибка при обработке видео: {ex.Message}";
            }
        }
        else
        {
            result = "❌ Необходимо указать URL видео с YouTube";
        }

        return result;
    }

    private async Task DownloadAndSendVideoAsync(
        string url,
        string videoTitle,
        Message message,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Получаем информацию о потоках видео
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(
                url,
                cancellationToken
            );

            // Выбираем лучший доступный видеопоток
            var bestStream = streamManifest.GetVideoStreams().GetWithHighestVideoQuality();

            // Скачиваем видео в память
            await using var videoStream = new MemoryStream();
            await _youtubeClient.Videos.Streams.CopyToAsync(
                bestStream,
                videoStream,
                null,
                cancellationToken
            );
            videoStream.Position = 0;

            // Генерируем имя файла
            var sanitizedTitle = SanitizeFileName(videoTitle);
            var fileName =
                $"{sanitizedTitle}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{bestStream.Container.Name}";

            await client.SendVideo(
                message.Chat,
                InputFile.FromStream(videoStream),
                "Имя файла: " + fileName,
                replyParameters: new ReplyParameters()
                {
                    ChatId = message.Chat,
                    MessageId = message.Id,
                },
                cancellationToken: cancellationToken
            );

            logger.LogInformation(
                "Видео {Title} успешно скачано и добавлено в хранилище",
                videoTitle
            );
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Скачивание видео отменено");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при скачивании видео {Url}", url);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        // Удаляем или заменяем недопустимые символы
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Empty;

        foreach (var c in fileName)
        {
            if (invalidChars.Contains(c))
            {
                sanitized += "_";
            }
            else
            {
                sanitized += c;
            }
        }

        // Обрезаем до максимальной длины
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}
