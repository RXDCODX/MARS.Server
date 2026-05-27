using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FFMpegCore;
using MARS.Server.Exstensions;
using MARS.Server.Services.PyroAlerts.Entitys;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.Twitch.Media;

public class TwitchMediaPreparationService(
    IWebHostEnvironment webHostEnvironment,
    Microsoft.EntityFrameworkCore.IDbContextFactory<DataBaseContext.AppDbContext> dbContextFactory,
    ILogger<TwitchMediaPreparationService> logger
) : ITwitchMediaPreparationService
{
    private const int MinimumAudioBitrateKbps = 128;
    private const int MinimumVideoBitrateKbps = 1200;
    private const int VideoFrameRate = 30;
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

    private static string NormalizePath(string filePath)
    {
        var result = Path.GetFullPath(filePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task<bool> ConvertAudioAsync(
        string sourceFilePath,
        string outputFilePath,
        CancellationToken cancellationToken
    )
    {
        var result = false;

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(outputFilePath));

            await FFMpegArguments
                .FromFileInput(sourceFilePath)
                .OutputToFile(
                    outputFilePath,
                    true,
                    options =>
                        options
                            .WithAudioCodec("libmp3lame")
                            .WithAudioBitrate(MinimumAudioBitrateKbps)
                            .WithCustomArgument("-vn")
                            .WithCustomArgument("-map_metadata -1")
                )
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();

            if (File.Exists(outputFilePath))
            {
                result = true;
                File.SetLastWriteTimeUtc(outputFilePath, DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ошибка конвертации аудио {FilePath}", sourceFilePath);
        }

        return result;
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

            await FFMpegArguments
                .FromFileInput(sourceFilePath)
                .OutputToFile(
                    outputFilePath,
                    true,
                    options =>
                        options
                            .WithVideoCodec("libx264")
                            .WithAudioCodec("aac")
                            .WithAudioBitrate(MinimumAudioBitrateKbps)
                            .WithConstantRateFactor(20)
                            .WithFramerate(VideoFrameRate)
                            .WithCustomArgument("-vf fps=30")
                            .WithCustomArgument("-pix_fmt yuv420p")
                            .WithCustomArgument("-preset veryfast")
                            .WithFastStart()
                )
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();

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
        double? RawFrameRate
    )> ReadProbeAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = (
            BitrateKbps: (long?)null,
            AverageFrameRate: (double?)null,
            RawFrameRate: (double?)null
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
                            : null
                    );
                }
                else if (primaryAudioStream is not null)
                {
                    result = (
                        BitrateKbps: primaryAudioStream.BitRate > 0
                            ? primaryAudioStream.BitRate / 1000
                            : null,
                        AverageFrameRate: null,
                        RawFrameRate: null
                    );
                }
                else if (analysis.Format.BitRate > 0)
                {
                    result = (
                        BitrateKbps: (long)Math.Round(analysis.Format.BitRate / 1000d),
                        AverageFrameRate: null,
                        RawFrameRate: null
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

    private static bool NeedsTranscoding(
        MediaType mediaType,
        (long? BitrateKbps, double? AverageFrameRate, double? RawFrameRate) probe
    )
    {
        var result = false;

        if (mediaType == MediaType.Audio)
        {
            result = probe.BitrateKbps is null || probe.BitrateKbps < MinimumAudioBitrateKbps;
        }
        else if (mediaType == MediaType.Video)
        {
            var hasLowBitrate =
                probe.BitrateKbps is null || probe.BitrateKbps < MinimumVideoBitrateKbps;
            var hasVariableFrameRate = IsVariableFrameRate(
                probe.AverageFrameRate,
                probe.RawFrameRate
            );
            result = hasLowBitrate || hasVariableFrameRate;
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

    private string GetTargetFilePath(string sourceFilePath, MediaType mediaType)
    {
        var result = sourceFilePath;

        if (mediaType == MediaType.Audio)
        {
            result = Path.ChangeExtension(sourceFilePath, ".mp3");
        }
        else if (mediaType == MediaType.Video)
        {
            result = Path.ChangeExtension(sourceFilePath, ".mp4");
        }

        return result;
    }

    private string GetCacheFilePath(string sourceFilePath, MediaType mediaType)
    {
        var cacheDirectory = Path.Combine(
            webHostEnvironment.WebRootPath,
            "Alerts",
            CacheFolderName
        );
        Directory.CreateDirectory(cacheDirectory);

        var key = string.Join(
            '|',
            sourceFilePath,
            File.GetLastWriteTimeUtc(sourceFilePath).Ticks,
            mediaType,
            MinimumAudioBitrateKbps,
            MinimumVideoBitrateKbps,
            VideoFrameRate
        );
        var hash = Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToLowerInvariant();
        var extension = mediaType == MediaType.Audio ? ".mp3" : ".mp4";

        var result = Path.Combine(cacheDirectory, hash + extension);
        return result;
    }

    private string GetConversionMessage(
        string sourceFilePath,
        MediaType mediaType,
        long? bitrateKbps
    )
    {
        var fileName = Path.GetFileName(sourceFilePath);
        var bitrateText = bitrateKbps is > 0 ? $"{bitrateKbps} kbps" : "unknown bitrate";
        var mediaKindText = mediaType == MediaType.Audio ? "аудио" : "видео";
        var result = $"Перекодирую {mediaKindText} {fileName} ({bitrateText}) для Chrome";

        if (mediaType == MediaType.Video)
        {
            result += $" и фиксирую {VideoFrameRate} fps";
        }

        return result;
    }

    private string BuildTranscodeReport(
        string sourceFilePath,
        string cachedFilePath,
        MediaType mediaType,
        (long? BitrateKbps, double? AverageFrameRate, double? RawFrameRate) probe
    )
    {
        var result = new StringBuilder();
        var fileName = Path.GetFileName(sourceFilePath);
        var mediaKindText = mediaType == MediaType.Audio ? "аудио" : "видео";
        var targetExtension = mediaType == MediaType.Audio ? ".mp3" : ".mp4";
        var detectedBitrateText = probe.BitrateKbps is > 0
            ? $"{probe.BitrateKbps} kbps"
            : "unknown";

        result.AppendLine($"Файл: {fileName}");
        result.AppendLine($"Тип: {mediaKindText}");
        result.AppendLine($"Исходник: {sourceFilePath}");
        result.AppendLine($"Кеш: {cachedFilePath}");
        result.AppendLine(
            $"Что сделано: создана кеш-копия с расширением {targetExtension}, оригинал заменён и удалён после успешной обработки"
        );
        result.AppendLine($"Подробности: исходная битрейт-оценка {detectedBitrateText}");

        if (mediaType == MediaType.Video)
        {
            var averageFrameRateText = probe.AverageFrameRate is > 0
                ? probe.AverageFrameRate.Value.ToString("0.##")
                : "unknown";
            var rawFrameRateText = probe.RawFrameRate is > 0
                ? probe.RawFrameRate.Value.ToString("0.##")
                : "unknown";

            result.AppendLine(
                $"Изменения: выставлен H.264, AAC, {MinimumVideoBitrateKbps} kbps, {VideoFrameRate} fps, fast start"
            );
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
        var mediaType = extension.GetFileMediaType();
        var cachePath = GetCacheFilePath(resolvedPath, mediaType);

        if (File.Exists(cachePath))
        {
            return BuildMediaInfo(cachePath, displayName);
        }

        var probe = await ReadProbeAsync(resolvedPath, cancellationToken);

        if (NeedsTranscoding(mediaType, probe))
        {
            var tempDirectory =
                Path.GetDirectoryName(resolvedPath) ?? webHostEnvironment.ContentRootPath;
            var tempFile = Path.Combine(
                tempDirectory,
                Guid.NewGuid().ToString() + (mediaType == MediaType.Audio ? ".mp3" : ".mp4")
            );

            try
            {
                bool converted;
                if (mediaType == MediaType.Audio)
                {
                    converted = await ConvertAudioAsync(resolvedPath, tempFile, cancellationToken);
                }
                else
                {
                    converted = await ConvertVideoAsync(resolvedPath, tempFile, cancellationToken);
                }

                if (converted && File.Exists(tempFile))
                {
                    try
                    {
                        if (File.Exists(resolvedPath))
                        {
                            File.Delete(resolvedPath);
                        }

                        File.Move(tempFile, resolvedPath);

                        if (File.Exists(cachePath))
                        {
                            File.Delete(cachePath);
                        }

                        File.Copy(resolvedPath, cachePath);
                        File.SetLastWriteTimeUtc(cachePath, DateTime.UtcNow);

                        await UpdateMemeOrderPathAsync(memeOrder, cachePath, cancellationToken);

                        if (onFileTranscoded is not null)
                        {
                            await onFileTranscoded(
                                BuildTranscodeReport(resolvedPath, cachePath, mediaType, probe)
                            );
                        }

                        return BuildMediaInfo(cachePath, displayName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to move converted file to cache {TempFile}",
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
