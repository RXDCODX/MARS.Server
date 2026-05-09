using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.SoundCloud;
using MARS.Server.Services.YouTube;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class DownloadCommand(
    YouTubeResolver youtubeResolver,
    SoundCloudResolver soundCloudResolver,
    ILogger<DownloadCommand> logger,
    ITelegramBotClient client
) : BaseCommand
{
    private readonly YoutubeClient _youtubeClient = new();

    public override string CommandName => "download";
    public override string Description => "Скачать трек/видео (YouTube, SoundCloud)";
    public override string[] Aliases => ["ytdownload", "dl"];
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];

    public override CommandVisibility Visibility => CommandVisibility.All;

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "url",
                Description = "URL видео с YouTube или SoundCloud",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "message",
                Description = "Message объект из телеграма",
                Type = nameof(Message),
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
                if (url.Contains("soundcloud", StringComparison.OrdinalIgnoreCase))
                {
                    var track = await soundCloudResolver.ResolveTrackAsync(url, cancellationToken);
                    if (track is not null)
                    {
                        var title = track.TrackName ?? track.Title;
                        result =
                            $"✅ Загрузка аудио начата: {title}\n⏳ Скачивание может занять время...";

                        _ = Task.Factory.StartNew(
                            () =>
                                DownloadAndSendAudioAsync(
                                    track,
                                    title,
                                    message,
                                    CancellationToken.None
                                ),
                            cancellationToken
                        );
                    }
                    else
                    {
                        result = "❌ Не удалось получить информацию о треке SoundCloud.";
                    }
                }
                else
                {
                    // YouTube - скачиваем видео и отправляем как видео
                    var videoInfo = await youtubeResolver.ResolveVideoAsync(url, cancellationToken);
                    if (videoInfo is not null)
                    {
                        result =
                            $"✅ Видео начинает обрабатываться: {videoInfo.TrackName}\n⏳ Скачивание может занять время...";

                        _ = Task.Factory.StartNew(
                            () =>
                                DownloadAndSendVideoAsync(
                                    url,
                                    videoInfo.TrackName,
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при обработке URL");
                result = $"❌ Ошибка при обработке: {ex.Message}";
            }
        }
        else
        {
            result = "❌ Необходимо указать URL видео/трека";
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

            // Выбираем лучший доступный видеопоток и аудиопоток
            var bestVideoStream = streamManifest.GetVideoStreams().GetWithHighestVideoQuality();
            var bestAudioStream = streamManifest.GetAudioStreams().GetWithHighestBitrate();

            var tempFile = Guid.NewGuid() + ".mp4";

            await _youtubeClient.Videos.DownloadAsync(
                [bestAudioStream, bestVideoStream],
                new ConversionRequest(
                    "ffmpeg",
                    tempFile,
                    Container.Mp4,
                    ConversionPreset.VerySlow,
                    new Dictionary<string, string?>()
                ),
                cancellationToken: cancellationToken
            );

            // Скачиваем видео в память
            await using var fileStream = File.OpenRead(tempFile);

            // Генерируем имя файла
            var sanitizedTitle = SanitizeFileName(videoTitle);
            var fileName = $"{sanitizedTitle}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4";

            try
            {
                await client.SendVideo(
                    message.Chat,
                    InputFile.FromStream(fileStream),
                    "Имя файла: " + fileName,
                    replyParameters: new ReplyParameters()
                    {
                        ChatId = message.Chat,
                        MessageId = message.Id,
                    },
                    cancellationToken: cancellationToken
                );

                logger.LogInformation("Видео {Title} успешно скачано и отправлено", videoTitle);
            }
            finally
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch { }
            }
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

    private async Task DownloadAndSendAudioAsync(
        BaseTrackInfo track,
        string title,
        Message message,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var outputDir = Path.Combine(Path.GetTempPath(), "mars-downloads");
            var filePath = await youtubeResolver.DownloadBestAudioStreamAsync(
                track,
                outputDir,
                cancellationToken
            );

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                logger.LogWarning("Не удалось скачать файл для {Url}", track.Url);
                return;
            }

            await using var fileStream = File.OpenRead(filePath);
            var sanitizedTitle = SanitizeFileName(title);
            var fileName =
                $"{sanitizedTitle}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(filePath)}";

            try
            {
                // Отправляем как аудио (музыку)
                await client.SendAudio(
                    message.Chat,
                    InputFile.FromStream(fileStream),
                    caption: "Имя файла: " + fileName,
                    replyParameters: new ReplyParameters()
                    {
                        ChatId = message.Chat,
                        MessageId = message.Id,
                    },
                    cancellationToken: cancellationToken
                );

                logger.LogInformation("Аудио {Title} успешно скачано и отправлено", title);
            }
            finally
            {
                try
                {
                    File.Delete(filePath);
                }
                catch { }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Скачивание аудио отменено");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при скачивании аудио {Url}", track?.Url);
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
