using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.PyroAlerts.Entitys;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Media;

public class TwitchMediaPreparationService(
    IWebHostEnvironment webHostEnvironment,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<TwitchMediaPreparationService> logger
) : ITwitchMediaPreparationService
{
    private const int MinimumAudioBitrateKbps = 128;
    private const int MinimumVideoBitrateKbps = 128;
    private const string CacheFolderName = "twitch_media_cache";

    private string? TryGetDevelopmentRandomMemeRootPath()
    {
        string? result = null;

        if (webHostEnvironment.IsProduction())
        {
            var currentDir = Directory.GetCurrentDirectory();
            var projectRoot = FindProjectRoot(currentDir);

            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                result = Path.Combine(projectRoot, "wwwroot", "Alerts", "random_meme");
            }
        }

        return result;
    }

    private static string? FindProjectRoot(string startPath)
    {
        var result = (string?)null;
        var dir = new DirectoryInfo(startPath);

        while (dir != null && result == null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                result = dir.FullName;
            }
            else
            {
                dir = dir.Parent;
            }
        }

        return result;
    }

    private async Task UpdateMemeOrderPathAsync(
        MemeOrder memeOrder,
        string newFilePath,
        bool isBrokenFile,
        CancellationToken cancellationToken
    )
    {
        memeOrder.FilePath = newFilePath;

        if (memeOrder.Id != Guid.Empty)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var entity = await dbContext.RandomMemeOrder.FirstOrDefaultAsync(
                x => x.Id == memeOrder.Id,
                cancellationToken
            );

            if (
                entity != null
                && !string.Equals(entity.FilePath, newFilePath, StringComparison.OrdinalIgnoreCase)
            )
            {
                entity.FilePath = newFilePath;
                entity.IsFileNotConvertable = isBrokenFile;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task UpdateAlertsForFileAsync(
        string oldAbsolutePath,
        string newAbsolutePath,
        string? displayName,
        MediaType mediaType,
        bool isBrokenFile,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var oldWebPath = BuildWebPath(oldAbsolutePath);
            var newWebPath = BuildWebPath(newAbsolutePath);

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var alerts = await dbContext
                .Alerts.Where(a =>
                    a.FileInfo.FilePath == oldWebPath
                    || a.FileInfo.FilePath == oldWebPath.TrimStart('/')
                )
                .ToListAsync(cancellationToken);

            if (alerts.Count == 0)
            {
                return;
            }

            foreach (var alert in alerts)
            {
                alert.FileInfo.FilePath = newWebPath;
                alert.FileInfo.FileName = Path.GetFileName(newAbsolutePath);
                alert.FileInfo.Extension = Path.GetExtension(newAbsolutePath);
                alert.FileInfo.Type = mediaType;
                alert.FileInfo.IsLocalFile = true;
                alert.FileInfo.IsFileNotConvertable = isBrokenFile;

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    alert.MetaInfo.DisplayName = displayName!;
                }

                dbContext.Entry(alert).State = EntityState.Modified;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Обновлено {Count} записей Alerts для файла {Old} -> {New}",
                alerts.Count,
                oldWebPath,
                newWebPath
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Не удалось обновить записи Alerts для файла {Old} -> {New}",
                oldAbsolutePath,
                newAbsolutePath
            );
        }
    }

    private async Task<bool> ConvertVideoAsync(
        string sourceFilePath,
        string outputFilePath,
        CancellationToken cancellationToken
    )
    {
        var result = false;

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(outputFilePath));

            var ext = Path.GetExtension(outputFilePath)?.ToLowerInvariant();

            var ffArgs = FFMpegArguments
                .FromFileInput(sourceFilePath)
                .OutputToFile(
                    outputFilePath,
                    true,
                    options =>
                    {
                        if (ext == ".webm")
                        {
                            options
                                .WithVideoCodec("libvpx")
                                .WithAudioCodec("aac")
                                .WithCustomArgument($"-b:v {MinimumVideoBitrateKbps}k")
                                .WithCustomArgument("-threads 2");
                        }
                        else
                        {
                            options
                                .WithVideoCodec("libx264")
                                .WithAudioCodec("libmp3lame")
                                .WithAudioBitrate(MinimumAudioBitrateKbps)
                                .WithConstantRateFactor(20)
                                .WithCustomArgument("-vf fps=30")
                                .WithCustomArgument("-pix_fmt yuv420p")
                                .WithCustomArgument("-preset veryfast")
                                .WithFastStart();
                        }
                    }
                )
                .CancellableThrough(cancellationToken);

            await ffArgs.ProcessAsynchronously();

            if (File.Exists(outputFilePath))
            {
                result = true;
                File.SetLastWriteTimeUtc(outputFilePath, DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ошибка конвертации видео {FilePath}", sourceFilePath);
        }

        return result;
    }

    private async Task<(
        long? BitrateKbps,
        double? AverageFrameRate,
        double? RawFrameRate,
        string? VideoCodecName,
        string? AudioCodecName
    )> ReadProbeAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = (
            BitrateKbps: (long?)null,
            AverageFrameRate: (double?)null,
            RawFrameRate: (double?)null,
            VideoCodecName: (string?)null,
            AudioCodecName: (string?)null
        );

        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            try
            {
                var analysis = await FFProbe.AnalyseAsync(
                    filePath,
                    cancellationToken: cancellationToken
                );
                var primaryVideoStream = analysis.PrimaryVideoStream;
                var primaryAudioStream = analysis.PrimaryAudioStream;

                if (primaryVideoStream is not null)
                {
                    result = (
                        BitrateKbps: primaryVideoStream.BitRate > 0
                            ? primaryVideoStream.BitRate / 1000
                            : null,
                        AverageFrameRate: primaryVideoStream.AverageFrameRate is > 0
                            ? primaryVideoStream.AverageFrameRate
                            : null,
                        RawFrameRate: primaryVideoStream.FrameRate is > 0
                            ? primaryVideoStream.FrameRate
                            : null,
                        VideoCodecName: NormalizeCodecName(primaryVideoStream.CodecName),
                        AudioCodecName: NormalizeCodecName(primaryAudioStream?.CodecName)
                    );
                }
                else if (primaryAudioStream is not null)
                {
                    result = (
                        BitrateKbps: primaryAudioStream.BitRate > 0
                            ? primaryAudioStream.BitRate / 1000
                            : null,
                        AverageFrameRate: null,
                        RawFrameRate: null,
                        VideoCodecName: null,
                        AudioCodecName: NormalizeCodecName(primaryAudioStream.CodecName)
                    );
                }
                else if (analysis.Format.BitRate > 0)
                {
                    result = (
                        BitrateKbps: (long)Math.Round(analysis.Format.BitRate / 1000d),
                        AverageFrameRate: null,
                        RawFrameRate: null,
                        VideoCodecName: null,
                        AudioCodecName: null
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "FFProbe analysis failed for file {FilePath}", filePath);
            }
        }

        return result;
    }

    private static string? NormalizeCodecName(string? codecName)
    {
        var result = string.IsNullOrWhiteSpace(codecName)
            ? null
            : codecName.Trim().ToLowerInvariant();

        return result;
    }

    private static bool NeedsTranscoding(
        MediaType mediaType,
        (
            long? BitrateKbps,
            double? AverageFrameRate,
            double? RawFrameRate,
            string? VideoCodecName,
            string? AudioCodecName
        ) probe,
        string targetExtension
    )
    {
        var result = false;

        if (mediaType == MediaType.Video && probe.BitrateKbps != null)
        {
            var hasLowBitrate = probe.BitrateKbps < MinimumVideoBitrateKbps;
            var hasVariableFrameRate = IsVariableFrameRate(
                probe.AverageFrameRate,
                probe.RawFrameRate
            );
            var ext = targetExtension?.ToLowerInvariant() ?? ".mp4";

            if (ext == ".webm")
            {
                // For webm we want VP8 video. Transcode if not VP8 or bitrate/frame issues.
                var needsVp8 = !string.Equals(
                    probe.VideoCodecName,
                    "vp8",
                    StringComparison.OrdinalIgnoreCase
                );

                result = hasLowBitrate || hasVariableFrameRate || needsVp8;
            }
            else
            {
                var needsH264Video = !string.Equals(
                    probe.VideoCodecName,
                    "h264",
                    StringComparison.OrdinalIgnoreCase
                );
                var needsMp3Audio =
                    probe.AudioCodecName is not null
                    && !string.Equals(
                        probe.AudioCodecName,
                        "mp3",
                        StringComparison.OrdinalIgnoreCase
                    );

                result = hasLowBitrate || hasVariableFrameRate || needsH264Video || needsMp3Audio;
            }
        }

        return result;
    }

    private static bool IsVariableFrameRate(double? averageFrameRate, double? rawFrameRate)
    {
        var result = false;

        if (averageFrameRate is > 0 && rawFrameRate is > 0)
        {
            result = Math.Abs(averageFrameRate.Value - rawFrameRate.Value) > 0.01;
        }

        return result;
    }

    private static string GetTargetFilePath(string sourceFilePath, MediaType mediaType)
    {
        var result = sourceFilePath;

        if (mediaType == MediaType.Video)
        {
            var ext = Path.GetExtension(sourceFilePath)?.ToLowerInvariant();

            // Preserve webm extension; otherwise use mp4 container for H.264
            if (string.Equals(ext, ".webm", StringComparison.OrdinalIgnoreCase))
            {
                result = Path.ChangeExtension(sourceFilePath, ".webm");
            }
            else
            {
                result = Path.ChangeExtension(sourceFilePath, ".mp4");
            }
        }

        return result;
    }

    private string GetCacheDirectoryPath()
    {
        var result = Path.Combine(webHostEnvironment.WebRootPath, "Alerts", CacheFolderName);
        return result;
    }

    private (int DeletedFiles, long FreedBytes) CleanupCacheDirectory(
        CancellationToken cancellationToken
    )
    {
        var result = (DeletedFiles: 0, FreedBytes: 0L);
        var cacheDirectory = GetCacheDirectoryPath();

        if (Directory.Exists(cacheDirectory))
        {
            var files = Directory.GetFiles(cacheDirectory);

            foreach (var filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var deletedSize = fileInfo.Length;
                    File.Delete(filePath);
                    result = (result.DeletedFiles + 1, result.FreedBytes + deletedSize);
                }
                catch (IOException ex)
                {
                    logger.LogDebug(ex, "Не удалось удалить кеш-файл {CacheFilePath}", filePath);
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.LogDebug(
                        ex,
                        "Нет прав для удаления кеш-файла {CacheFilePath}",
                        filePath
                    );
                }
            }
        }

        return result;
    }

    private static string BuildTranscodeReport(
        string sourceFilePath,
        string targetFilePath,
        MediaType mediaType,
        (
            long? BitrateKbps,
            double? AverageFrameRate,
            double? RawFrameRate,
            string? VideoCodecName,
            string? AudioCodecName
        ) probe
    )
    {
        var result = new StringBuilder();
        var fileName = Path.GetFileName(sourceFilePath);
        var mediaKindText = "видео";
        var targetExtension = Path.GetExtension(sourceFilePath);
        var detectedBitrateText = probe.BitrateKbps is > 0
            ? $"{probe.BitrateKbps} kbps"
            : "unknown";

        result.AppendLine($"Файл: {fileName}");
        result.AppendLine($"Тип: {mediaKindText}");
        result.AppendLine($"Исходник: {sourceFilePath}");
        result.AppendLine($"Результат: {targetFilePath}");
        result.AppendLine(
            $"Что сделано: оригинал заменён на файл с расширением {targetExtension} после успешной обработки"
        );
        result.AppendLine($"Подробности: исходная битрейт-оценка {detectedBitrateText}");
        result.AppendLine(
            $"Кодеки источника: video={probe.VideoCodecName ?? "unknown"}, audio={probe.AudioCodecName ?? "unknown"}"
        );

        if (mediaType == MediaType.Video)
        {
            var averageFrameRateText = probe.AverageFrameRate is > 0
                ? probe.AverageFrameRate.Value.ToString("0.##")
                : "unknown";
            var rawFrameRateText = probe.RawFrameRate is > 0
                ? probe.RawFrameRate.Value.ToString("0.##")
                : "unknown";

            result.AppendLine($"Кадры: average={averageFrameRateText}, raw={rawFrameRateText}");
        }
        else
        {
            result.AppendLine(
                $"Изменения: выставлен MP3, {MinimumAudioBitrateKbps} kbps, убраны видео-данные и метаданные"
            );
        }

        return result.ToString().TrimEnd();
    }

    private MediaInfo BuildMediaInfo(string filePath, string? displayName)
    {
        var result = new MediaInfo
        {
            FileInfo = new MediaFileInfo
            {
                Extension = Path.GetExtension(filePath),
                Type = Path.GetExtension(filePath).GetFileMediaType(),
                FileName = Path.GetFileName(filePath),
                FilePath = BuildWebPath(filePath),
                IsLocalFile = true,
            },
            MetaInfo = new MediaMetaInfo
            {
                DisplayName = displayName ?? string.Empty,
                IsLooped = false,
            },
            PositionInfo = new MediaPositionInfo
            {
                Height = 400,
                Width = 400,
                IsProportion = true,
                IsResizeRequires = true,
            },
            StylesInfo = new MediaStylesInfo { IsBorder = false },
            TextInfo = new MediaTextInfo(),
        };

        return result;
    }

    private string BuildWebPath(string filePath)
    {
        var result =
            "/"
            + Path.GetRelativePath(webHostEnvironment.WebRootPath, Path.GetFullPath(filePath))
                .Replace('\\', '/');

        return result;
    }

    private string ResolveFilePath(string filePath)
    {
        var result = filePath;

        if (!string.IsNullOrWhiteSpace(filePath) && !Path.IsPathRooted(filePath))
        {
            result = Path.Combine(webHostEnvironment.WebRootPath, filePath);
        }

        return result;
    }

    private static void EnsureDirectoryExists(string? directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    public async Task<MediaInfo?> PrepareMediaAsync(
        MemeOrder? memeOrder,
        string? displayName,
        CancellationToken cancellationToken = default,
        Func<string, Task>? onFileTranscoded = null
    )
    {
        if (memeOrder is null || string.IsNullOrWhiteSpace(memeOrder.FilePath))
        {
            return null;
        }

        var resolvedPath = ResolveFilePath(memeOrder.FilePath!);

        if (!File.Exists(resolvedPath))
        {
            var devRoot = TryGetDevelopmentRandomMemeRootPath();
            if (!string.IsNullOrWhiteSpace(devRoot))
            {
                var candidate = Path.Combine(devRoot, memeOrder.FilePath!);
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                }
            }
        }

        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        var extension = Path.GetExtension(resolvedPath);
        var mediaType = await extension.GetFileMediaTypeAsync();
        var targetPath = GetTargetFilePath(resolvedPath, mediaType);
        var cleanupResult = CleanupCacheDirectory(cancellationToken);

        if (cleanupResult.DeletedFiles > 0)
        {
            logger.LogInformation(
                "Очистка twitch_media_cache завершена: удалено файлов {DeletedFiles}, освобождено байт {FreedBytes}",
                cleanupResult.DeletedFiles,
                cleanupResult.FreedBytes
            );
        }

        var probe = await ReadProbeAsync(resolvedPath, cancellationToken);
        var targetExt = Path.GetExtension(targetPath)?.ToLowerInvariant() ?? ".mp4";

        if (NeedsTranscoding(mediaType, probe, targetExt))
        {
            var sourcePathForReport = resolvedPath;
            var tempDirectory =
                Path.GetDirectoryName(resolvedPath) ?? webHostEnvironment.ContentRootPath;
            var tempExtension = Path.GetExtension(targetPath);
            var tempFile = Path.Combine(
                tempDirectory,
                Guid.NewGuid() + (string.IsNullOrWhiteSpace(tempExtension) ? ".mp4" : tempExtension)
            );

            try
            {
                var converted = await ConvertVideoAsync(resolvedPath, tempFile, cancellationToken);

                if (converted && File.Exists(tempFile))
                {
                    try
                    {
                        if (File.Exists(resolvedPath))
                        {
                            File.Delete(resolvedPath);
                        }

                        if (
                            !string.Equals(
                                resolvedPath,
                                targetPath,
                                StringComparison.OrdinalIgnoreCase
                            ) && File.Exists(targetPath)
                        )
                        {
                            File.Delete(targetPath);
                        }

                        File.Move(tempFile, targetPath);

                        var isBrokenFile = false;
                        var convertedProbe = await ReadProbeAsync(targetPath, cancellationToken);

                        if (NeedsTranscoding(mediaType, convertedProbe, extension))
                        {
                            isBrokenFile = true;
                        }

                        await UpdateMemeOrderPathAsync(
                            memeOrder,
                            targetPath,
                            isBrokenFile,
                            cancellationToken
                        );

                        // Update any Alerts entries that referenced the old file path
                        try
                        {
                            await UpdateAlertsForFileAsync(
                                resolvedPath,
                                targetPath,
                                displayName,
                                mediaType,
                                isBrokenFile,
                                cancellationToken
                            );
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, "Ошибка при обновлении Alerts после конвертации");
                        }

                        var postCleanupResult = CleanupCacheDirectory(cancellationToken);

                        if (postCleanupResult.DeletedFiles > 0)
                        {
                            logger.LogInformation(
                                "После конвертации удалены кеш-файлы: {DeletedFiles}, освобождено байт {FreedBytes}",
                                postCleanupResult.DeletedFiles,
                                postCleanupResult.FreedBytes
                            );
                        }

                        if (onFileTranscoded is not null)
                        {
                            await onFileTranscoded(
                                BuildTranscodeReport(
                                    sourcePathForReport,
                                    targetPath,
                                    mediaType,
                                    probe
                                )
                            );
                        }

                        return BuildMediaInfo(targetPath, displayName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to move converted file to target path {TempFile}",
                            tempFile
                        );
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        return BuildMediaInfo(resolvedPath, displayName);
    }
}
