using DSharpPlus;
using DSharpPlus.Entities;
using FFMpegCore;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.SoundCloud;
using MARS.Server.Services.YouTube;
using Telegram.Bot;
using Telegram.Bot.Types;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class DownloadCommand(
    YouTubeResolver youtubeResolver,
    SoundCloudResolver soundCloudResolver,
    ILogger<DownloadCommand> logger,
    ITelegramBotClient client,
    IDiscordGatewayService discordGatewayService
) : BaseCommand
{
    private const long MaxVideoSizeBytes = 20L * 1024 * 1024;
    private const long MaxDiscordFileSizeBytes = 10L * 1024 * 1024;
    internal const long FinalFallbackSizeBytes = 19L * 1024 * 1024;
    internal const long DiscordFinalFallbackSizeBytes = 9L * 1024 * 1024;

    private static readonly VideoCompressionProfile[] VideoCompressionProfiles =
    [
        new(1280, 28, 128),
        new(960, 30, 112),
        new(854, 32, 96),
        new(640, 34, 80),
    ];

    private readonly YoutubeClient _youtubeClient = new();

    public override string CommandName => "download";
    public override string Description => "Скачать трек/видео (YouTube, SoundCloud)";
    public override string[] Aliases => ["ytdownload", "dl"];
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Discord];

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
                Required = false,
            },
            new()
            {
                Name = "discord_channel_id",
                Description = "Discord channel ID",
                Type = "ulong",
                Required = false,
            },
            new()
            {
                Name = "discord_message_id",
                Description = "Discord message ID для reply",
                Type = "ulong",
                Required = false,
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

        if (!hasUrl)
        {
            return "❌ Необходимо указать URL видео/трека";
        }

        var url = urlObj!.ToString()!.Trim();

        try
        {
            if (platform == Platform.Discord)
            {
                result = await HandleDiscordAsync(url, parameters, cancellationToken);
            }
            else if (
                parameters.TryGetValue("message", out var messageObj)
                && messageObj is Message message
            )
            {
                result = await HandleTelegramAsync(url, message, cancellationToken);
            }
            else
            {
                result = "❌ Не удалось получить контекст сообщения";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке URL");
            result = $"❌ Ошибка при обработке: {ex.Message}";
        }

        return result;
    }

    private async Task<string> HandleTelegramAsync(
        string url,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var result = "Ошибка обработки видео";

        if (url.Contains("soundcloud", StringComparison.OrdinalIgnoreCase))
        {
            var track = await soundCloudResolver.ResolveTrackAsync(url, cancellationToken);
            if (track is not null)
            {
                var title = track.TrackName;
                result = $"✅ Загрузка аудио начата: {title}\n⏳ Скачивание может занять время...";

                _ = Task.Factory.StartNew(
                    () => DownloadAndSendAudioAsync(track, title, message, CancellationToken.None),
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

        return result;
    }

    private async Task<string> HandleDiscordAsync(
        string url,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken
    )
    {
        var result = "Ошибка обработки видео";

        ulong channelId = 0;
        ulong? messageId = null;

        if (
            parameters.TryGetValue("discord_channel_id", out var channelIdObj)
            && channelIdObj is ulong cid
        )
        {
            channelId = cid;
        }

        if (
            parameters.TryGetValue("discord_message_id", out var messageIdObj)
            && messageIdObj is ulong mid
        )
        {
            messageId = mid;
        }

        if (channelId == 0)
        {
            return "❌ Не удалось получить контекст Discord канала";
        }

        var discordClient = discordGatewayService.Client;
        if (discordClient is null)
        {
            return "❌ Discord клиент не подключен";
        }

        var channel = await discordClient.GetChannelAsync(channelId);

        if (url.Contains("soundcloud", StringComparison.OrdinalIgnoreCase))
        {
            var track = await soundCloudResolver.ResolveTrackAsync(url, cancellationToken);
            if (track is not null)
            {
                var title = track.TrackName;
                result = $"✅ Загрузка аудио начата: {title}\n⏳ Скачивание может занять время...";

                _ = Task.Factory.StartNew(
                    () =>
                        DownloadAndSendDiscordAudioAsync(
                            track,
                            title,
                            discordClient,
                            channel,
                            messageId,
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
            var videoInfo = await youtubeResolver.ResolveVideoAsync(url, cancellationToken);
            if (videoInfo is not null)
            {
                result =
                    $"✅ Видео начинает обрабатываться: {videoInfo.TrackName}\n⏳ Скачивание может занять время...";

                _ = Task.Factory.StartNew(
                    () =>
                        DownloadAndSendDiscordVideoAsync(
                            url,
                            videoInfo.TrackName,
                            discordClient,
                            channel,
                            messageId,
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

            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "mars-downloads",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(tempDirectory);

            var videoStreamFile = Path.Combine(tempDirectory, Guid.NewGuid() + ".mp4");
            var audioStreamFile = Path.Combine(tempDirectory, Guid.NewGuid() + ".m4a");
            var tempFile = Path.Combine(tempDirectory, Guid.NewGuid() + ".mp4");
            var preparedFile = tempFile;

            await _youtubeClient.Videos.Streams.DownloadAsync(
                bestVideoStream,
                videoStreamFile,
                cancellationToken: cancellationToken
            );

            await _youtubeClient.Videos.Streams.DownloadAsync(
                bestAudioStream,
                audioStreamFile,
                cancellationToken: cancellationToken
            );

            FFMpeg.ReplaceAudio(videoStreamFile, audioStreamFile, tempFile);

            preparedFile = await PrepareVideoForTelegramAsync(tempFile, cancellationToken);

            if (preparedFile is null)
            {
                await client.SendMessage(
                    message.Chat,
                    "❌ Не удалось сжать видео до 20 МБ. Файл слишком большой для Telegram.",
                    replyParameters: new ReplyParameters()
                    {
                        ChatId = message.Chat,
                        MessageId = message.Id,
                    },
                    cancellationToken: cancellationToken
                );

                CleanupFiles(videoStreamFile, audioStreamFile, tempFile, null);
                return;
            }

            await using var fileStream = File.OpenRead(preparedFile);

            // Генерируем имя файла
            var sanitizedTitle = SanitizeFileName(videoTitle);
            var fileName = $"{sanitizedTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";

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
                CleanupFiles(videoStreamFile, audioStreamFile, tempFile, preparedFile);
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

    private async Task<string?> PrepareVideoForTelegramAsync(
        string sourceFile,
        CancellationToken cancellationToken
    )
    {
        string? result = null;

        if (new FileInfo(sourceFile).Length > MaxVideoSizeBytes)
        {
            foreach (var profile in VideoCompressionProfiles)
            {
                var compressedFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
                var compressionSucceeded = await TryTranscodeVideoAsync(
                    sourceFile,
                    compressedFile,
                    profile,
                    cancellationToken
                );

                if (
                    compressionSucceeded
                    && new FileInfo(compressedFile).Length <= MaxVideoSizeBytes
                )
                {
                    result = compressedFile;
                    break;
                }

                try
                {
                    File.Delete(compressedFile);
                }
                catch { }
            }

            if (result is null)
            {
                var trimmedFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
                var fallbackSucceeded = await TryTranscodeVideoAsync(
                    sourceFile,
                    trimmedFile,
                    VideoCompressionProfiles[^1],
                    cancellationToken,
                    FinalFallbackSizeBytes
                );

                if (fallbackSucceeded && new FileInfo(trimmedFile).Length <= MaxVideoSizeBytes)
                {
                    result = trimmedFile;
                }
                else
                {
                    try
                    {
                        File.Delete(trimmedFile);
                    }
                    catch { }
                }
            }
        }
        else
        {
            result = sourceFile;
        }

        return result;
    }

    private async Task<bool> TryTranscodeVideoAsync(
        string inputFile,
        string outputFile,
        VideoCompressionProfile profile,
        CancellationToken cancellationToken,
        long? maxOutputSizeBytes = null
    )
    {
        var result = false;

        try
        {
            var outputBuilder = FFMpegArguments
                .FromFileInput(inputFile)
                .OutputToFile(
                    outputFile,
                    true,
                    options =>
                        options
                            .WithVideoCodec("libx264")
                            .WithAudioCodec("aac")
                            .WithAudioBitrate(profile.AudioBitrateKbps)
                            .WithConstantRateFactor(profile.Crf)
                            .WithVideoFilters(filterOptions =>
                                filterOptions.Scale(profile.MaxWidth, -2)
                            )
                            .WithCustomArgument("-pix_fmt yuv420p")
                            .WithCustomArgument("-preset veryfast")
                            .WithFastStart()
                )
                .CancellableThrough(cancellationToken);

            if (maxOutputSizeBytes is not null)
            {
                outputBuilder = FFMpegArguments
                    .FromFileInput(inputFile)
                    .OutputToFile(
                        outputFile,
                        true,
                        options =>
                            options
                                .WithVideoCodec("libx264")
                                .WithAudioCodec("aac")
                                .WithAudioBitrate(profile.AudioBitrateKbps)
                                .WithConstantRateFactor(profile.Crf)
                                .WithVideoFilters(filterOptions =>
                                    filterOptions.Scale(profile.MaxWidth, -2)
                                )
                                .WithCustomArgument("-pix_fmt yuv420p")
                                .WithCustomArgument("-preset veryfast")
                                .WithCustomArgument($"-fs {maxOutputSizeBytes.Value}")
                                .WithFastStart()
                    )
                    .CancellableThrough(cancellationToken);
            }

            await outputBuilder.ProcessAsynchronously();

            if (File.Exists(outputFile))
            {
                result = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "FFMpegCore не смог подготовить видео {InputFile} -> {OutputFile}",
                inputFile,
                outputFile
            );
        }

        return result;
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

            var preparedFile = await PrepareAudioForTelegramAsync(filePath, cancellationToken);

            if (preparedFile is null)
            {
                await client.SendMessage(
                    message.Chat,
                    "❌ Не удалось сжать аудио до 20 МБ. Файл слишком большой для Telegram.",
                    replyParameters: new ReplyParameters()
                    {
                        ChatId = message.Chat,
                        MessageId = message.Id,
                    },
                    cancellationToken: cancellationToken
                );
                try
                {
                    File.Delete(filePath);
                }
                catch { }
                return;
            }

            await using var fileStream = File.OpenRead(preparedFile);
            var sanitizedTitle = SanitizeFileName(title);
            var fileName =
                $"{sanitizedTitle}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(preparedFile)}";

            try
            {
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
                    if (!string.Equals(preparedFile, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(preparedFile);
                    }
                }
                catch { }

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

    private async Task<string?> PrepareAudioForTelegramAsync(
        string sourceFile,
        CancellationToken cancellationToken
    )
    {
        string? result = null;

        if (new FileInfo(sourceFile).Length > MaxVideoSizeBytes)
        {
            var profiles = AudioCompressionProfileFactory.CreateProfiles();

            foreach (var (bitrateKbps, maxSizeBytes) in profiles)
            {
                var compressedFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp3");

                var succeeded = await TryTranscodeAudioAsync(
                    sourceFile,
                    compressedFile,
                    bitrateKbps,
                    maxSizeBytes,
                    cancellationToken
                );

                if (succeeded && new FileInfo(compressedFile).Length <= MaxVideoSizeBytes)
                {
                    result = compressedFile;
                    break;
                }

                try
                {
                    File.Delete(compressedFile);
                }
                catch { }
            }
        }
        else
        {
            result = sourceFile;
        }

        return result;
    }

    private async Task<bool> TryTranscodeAudioAsync(
        string inputFile,
        string outputFile,
        int bitrateKbps,
        long? maxOutputSizeBytes,
        CancellationToken cancellationToken
    )
    {
        var result = false;

        try
        {
            var outputBuilder = FFMpegArguments
                .FromFileInput(inputFile)
                .OutputToFile(
                    outputFile,
                    true,
                    options =>
                        options
                            .WithAudioCodec("libmp3lame")
                            .WithAudioBitrate(bitrateKbps)
                            .WithCustomArgument("-y")
                )
                .CancellableThrough(cancellationToken);

            if (maxOutputSizeBytes is not null)
            {
                outputBuilder = FFMpegArguments
                    .FromFileInput(inputFile)
                    .OutputToFile(
                        outputFile,
                        true,
                        options =>
                            options
                                .WithAudioCodec("libmp3lame")
                                .WithAudioBitrate(bitrateKbps)
                                .WithCustomArgument($"-fs {maxOutputSizeBytes.Value}")
                                .WithCustomArgument("-y")
                    )
                    .CancellableThrough(cancellationToken);
            }

            await outputBuilder.ProcessAsynchronously();

            if (File.Exists(outputFile) && new FileInfo(outputFile).Length > 0)
            {
                result = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "FFMpegCore не смог подготовить аудио {InputFile} -> {OutputFile}",
                inputFile,
                outputFile
            );
        }

        return result;
    }

    private async Task DownloadAndSendDiscordVideoAsync(
        string url,
        string videoTitle,
        DiscordClient discordClient,
        DiscordChannel channel,
        ulong? replyMessageId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(
                url,
                cancellationToken
            );

            var bestVideoStream = streamManifest.GetVideoStreams().GetWithHighestVideoQuality();
            var bestAudioStream = streamManifest.GetAudioStreams().GetWithHighestBitrate();

            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "mars-downloads",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(tempDirectory);

            var videoStreamFile = Path.Combine(tempDirectory, Guid.NewGuid() + ".mp4");
            var audioStreamFile = Path.Combine(tempDirectory, Guid.NewGuid() + ".m4a");
            var tempFile = Path.Combine(tempDirectory, Guid.NewGuid() + ".mp4");
            var preparedFile = tempFile;

            await _youtubeClient.Videos.Streams.DownloadAsync(
                bestVideoStream,
                videoStreamFile,
                cancellationToken: cancellationToken
            );

            await _youtubeClient.Videos.Streams.DownloadAsync(
                bestAudioStream,
                audioStreamFile,
                cancellationToken: cancellationToken
            );

            FFMpeg.ReplaceAudio(videoStreamFile, audioStreamFile, tempFile);

            preparedFile = await PrepareVideoForDiscordAsync(tempFile, cancellationToken);

            if (preparedFile is null)
            {
                var builder = new DiscordMessageBuilder();
                builder.WithContent(
                    "❌ Не удалось сжать видео до 10 МБ. Файл слишком большой для Discord."
                );
                if (replyMessageId.HasValue)
                {
                    builder.WithReply(replyMessageId.Value, false, false);
                }

                await discordClient.SendMessageAsync(channel, builder);

                CleanupFiles(videoStreamFile, audioStreamFile, tempFile, null);
                return;
            }

            await using var fileStream = File.OpenRead(preparedFile);

            var sanitizedTitle = SanitizeFileName(videoTitle);
            var fileName = $"{sanitizedTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";

            var sendBuilder = new DiscordMessageBuilder();
            sendBuilder.WithContent($"Имя файла: {fileName}");
            if (replyMessageId.HasValue)
            {
                sendBuilder.WithReply(replyMessageId.Value, false, false);
            }
            sendBuilder.AddFile(fileName, fileStream, true);

            await discordClient.SendMessageAsync(channel, sendBuilder);

            logger.LogInformation(
                "Видео {Title} успешно скачано и отправлено в Discord",
                videoTitle
            );

            CleanupFiles(videoStreamFile, audioStreamFile, tempFile, preparedFile);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Скачивание видео отменено");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при скачивании видео {Url} для Discord", url);
        }
    }

    private async Task DownloadAndSendDiscordAudioAsync(
        BaseTrackInfo track,
        string title,
        DiscordClient discordClient,
        DiscordChannel channel,
        ulong? replyMessageId,
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

            var preparedFile = await PrepareAudioForDiscordAsync(filePath, cancellationToken);

            if (preparedFile is null)
            {
                var builder = new DiscordMessageBuilder();
                builder.WithContent(
                    "❌ Не удалось сжать аудио до 10 МБ. Файл слишком большой для Discord."
                );
                if (replyMessageId.HasValue)
                {
                    builder.WithReply(replyMessageId.Value, false, false);
                }

                await discordClient.SendMessageAsync(channel, builder);

                try
                {
                    File.Delete(filePath);
                }
                catch { }
                return;
            }

            await using var fileStream = File.OpenRead(preparedFile);
            var sanitizedTitle = SanitizeFileName(title);
            var fileName =
                $"{sanitizedTitle}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(preparedFile)}";

            var sendBuilder = new DiscordMessageBuilder();
            sendBuilder.WithContent($"Имя файла: {fileName}");
            if (replyMessageId.HasValue)
            {
                sendBuilder.WithReply(replyMessageId.Value, false, false);
            }
            sendBuilder.AddFile(fileName, fileStream, true);

            await discordClient.SendMessageAsync(channel, sendBuilder);

            logger.LogInformation("Аудио {Title} успешно скачано и отправлено в Discord", title);

            try
            {
                if (!string.Equals(preparedFile, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(preparedFile);
                }
            }
            catch { }

            try
            {
                File.Delete(filePath);
            }
            catch { }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Скачивание аудио отменено");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при скачивании аудио {Url} для Discord", track?.Url);
        }
    }

    private async Task<string?> PrepareVideoForDiscordAsync(
        string sourceFile,
        CancellationToken cancellationToken
    )
    {
        string? result = null;

        if (new FileInfo(sourceFile).Length > MaxDiscordFileSizeBytes)
        {
            foreach (var profile in VideoCompressionProfiles)
            {
                var compressedFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
                var compressionSucceeded = await TryTranscodeVideoAsync(
                    sourceFile,
                    compressedFile,
                    profile,
                    cancellationToken
                );

                if (
                    compressionSucceeded
                    && new FileInfo(compressedFile).Length <= MaxDiscordFileSizeBytes
                )
                {
                    result = compressedFile;
                    break;
                }

                try
                {
                    File.Delete(compressedFile);
                }
                catch { }
            }

            if (result is null)
            {
                var trimmedFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
                var fallbackSucceeded = await TryTranscodeVideoAsync(
                    sourceFile,
                    trimmedFile,
                    VideoCompressionProfiles[^1],
                    cancellationToken,
                    DiscordFinalFallbackSizeBytes
                );

                if (
                    fallbackSucceeded
                    && new FileInfo(trimmedFile).Length <= MaxDiscordFileSizeBytes
                )
                {
                    result = trimmedFile;
                }
                else
                {
                    try
                    {
                        File.Delete(trimmedFile);
                    }
                    catch { }
                }
            }
        }
        else
        {
            result = sourceFile;
        }

        return result;
    }

    private async Task<string?> PrepareAudioForDiscordAsync(
        string sourceFile,
        CancellationToken cancellationToken
    )
    {
        string? result = null;

        if (new FileInfo(sourceFile).Length > MaxDiscordFileSizeBytes)
        {
            var profiles = AudioCompressionProfileFactory.CreateDiscordProfiles();

            foreach (var (bitrateKbps, maxSizeBytes) in profiles)
            {
                var compressedFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp3");

                var succeeded = await TryTranscodeAudioAsync(
                    sourceFile,
                    compressedFile,
                    bitrateKbps,
                    maxSizeBytes,
                    cancellationToken
                );

                if (succeeded && new FileInfo(compressedFile).Length <= MaxDiscordFileSizeBytes)
                {
                    result = compressedFile;
                    break;
                }

                try
                {
                    File.Delete(compressedFile);
                }
                catch { }
            }
        }
        else
        {
            result = sourceFile;
        }

        return result;
    }

    private static void CleanupFiles(
        string? videoStreamFile,
        string? audioStreamFile,
        string? tempFile,
        string? preparedFile
    )
    {
        try
        {
            if (!string.IsNullOrEmpty(videoStreamFile) && File.Exists(videoStreamFile))
            {
                File.Delete(videoStreamFile);
            }

            if (!string.IsNullOrEmpty(audioStreamFile) && File.Exists(audioStreamFile))
            {
                File.Delete(audioStreamFile);
            }

            if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            if (
                !string.IsNullOrEmpty(preparedFile)
                && !string.Equals(preparedFile, tempFile, StringComparison.OrdinalIgnoreCase)
                && File.Exists(preparedFile)
            )
            {
                File.Delete(preparedFile);
            }
        }
        catch { }
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

    private sealed record VideoCompressionProfile(int MaxWidth, int Crf, int AudioBitrateKbps);
}

file static class AudioCompressionProfileFactory
{
    private static readonly int[] BitrateKbpsSequence = [96, 64, 48, 32];

    public static IReadOnlyList<(int BitrateKbps, long? MaxSizeBytes)> CreateProfiles()
    {
        var profiles = new List<(int BitrateKbps, long? MaxSizeBytes)>();

        foreach (var bitrate in BitrateKbpsSequence)
        {
            profiles.Add((bitrate, null));
        }

        profiles.Add((32, DownloadCommand.FinalFallbackSizeBytes));

        return profiles;
    }

    public static IReadOnlyList<(int BitrateKbps, long? MaxSizeBytes)> CreateDiscordProfiles()
    {
        var profiles = new List<(int BitrateKbps, long? MaxSizeBytes)>();

        foreach (var bitrate in BitrateKbpsSequence)
        {
            profiles.Add((bitrate, null));
        }

        profiles.Add((32, DownloadCommand.DiscordFinalFallbackSizeBytes));

        return profiles;
    }
}
