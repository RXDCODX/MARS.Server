using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MARS.Server.Exstensions;
using MARS.Server.Services.PyroAlerts.Entitys;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.Twitch.Media;

public class TwitchMediaPreparationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWebHostEnvironment webHostEnvironment,
    ILogger<TwitchMediaPreparationService> logger
) : ITwitchMediaPreparationService
{
    private const int MinimumAudioBitrateKbps = 128;
    private const int MinimumVideoBitrateKbps = 128;
    private const int VideoFrameRate = 30;
    private const string CacheFolderName = "_converted";
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new(
        StringComparer.OrdinalIgnoreCase
    );

    public async Task<MediaInfo?> PrepareMediaAsync(
        MemeOrder memeOrder,
        string? displayName,
        CancellationToken cancellationToken = default,
        Func<string, Task>? onFileTranscoded = null
    )
    {
        MediaInfo? result = null;

        var resolvedFilePath = ResolveFilePath(memeOrder.FilePath);

        if (!string.IsNullOrWhiteSpace(resolvedFilePath) && File.Exists(resolvedFilePath))
        {
            var sourceFilePath = Path.GetFullPath(resolvedFilePath);
            var fileLock = _fileLocks.GetOrAdd(sourceFilePath, _ => new SemaphoreSlim(1, 1));

            await fileLock.WaitAsync(cancellationToken);
            try
            {
                var currentFilePath = ResolveFilePath(memeOrder.FilePath);
                if (File.Exists(currentFilePath))
                {
                    var mediaType = await Path.GetExtension(currentFilePath)
                        .GetFileMediaTypeAsync();
                    var playableFilePath = currentFilePath;

                    if (mediaType is MediaType.Audio or MediaType.Video)
                    {
                        var probe = await ReadProbeAsync(currentFilePath, cancellationToken);
                        if (NeedsTranscoding(mediaType, probe))
                        {
                            playableFilePath = await ReplaceWithTranscodedMediaAsync(
                                memeOrder,
                                currentFilePath,
                                mediaType,
                                probe.BitrateKbps,
                                cancellationToken,
                                onFileTranscoded
                            );
                        }
                    }

                    result = BuildMediaInfo(playableFilePath, displayName);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Не удалось подготовить медиафайл {FilePath}",
                    memeOrder.FilePath
                );
            }
            finally
            {
                fileLock.Release();
            }
        }

        return result;
    }

    private async Task<string> ReplaceWithTranscodedMediaAsync(
        MemeOrder memeOrder,
        string sourceFilePath,
        MediaType mediaType,
        long? bitrateKbps,
        CancellationToken cancellationToken,
        Func<string, Task>? onFileTranscoded
    )
    {
        var result = sourceFilePath;
        var targetFilePath = GetTargetFilePath(sourceFilePath, mediaType);

        if (await IsTranscodedVersionReadyAsync(sourceFilePath, targetFilePath))
        {
            await ReplaceOriginalPathWithTargetAsync(
                memeOrder,
                sourceFilePath,
                targetFilePath,
                cancellationToken
            );
            result = targetFilePath;
        }
        else
        {
            logger.LogInformation(GetConversionMessage(sourceFilePath, mediaType, bitrateKbps));

            var tempFilePath = GetCacheFilePath(sourceFilePath, mediaType);
            var transcodeSucceeded =
                mediaType == MediaType.Audio
                    ? await ConvertAudioAsync(sourceFilePath, tempFilePath, cancellationToken)
                    : await ConvertVideoAsync(sourceFilePath, tempFilePath, cancellationToken);

            if (transcodeSucceeded)
            {
                await ReplaceTranscodedFileAsync(
                    memeOrder,
                    sourceFilePath,
                    targetFilePath,
                    tempFilePath,
                    cancellationToken
                );

                if (onFileTranscoded != null)
                {
                    var transcodedFileName = Path.GetFileName(targetFilePath);
                    await onFileTranscoded($"Сконвертирован файл: {transcodedFileName}");
                }

                result = targetFilePath;
            }
        }

        return result;
    }

    private async Task<bool> IsTranscodedVersionReadyAsync(
        string sourceFilePath,
        string targetFilePath
    )
    {
        var result = false;

        if (
            File.Exists(targetFilePath)
            && File.GetLastWriteTimeUtc(targetFilePath) >= File.GetLastWriteTimeUtc(sourceFilePath)
        )
        {
            result = true;
        }

        await Task.CompletedTask;
        return result;
    }

    private async Task ReplaceOriginalPathWithTargetAsync(
        MemeOrder memeOrder,
        string sourceFilePath,
        string targetFilePath,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(sourceFilePath, targetFilePath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(sourceFilePath))
            {
                File.Delete(sourceFilePath);
            }

            await UpdateMemeOrderPathAsync(memeOrder, targetFilePath, cancellationToken);
        }

        await SyncDevelopmentCopyAsync(sourceFilePath, targetFilePath);
    }

    private async Task ReplaceTranscodedFileAsync(
        MemeOrder memeOrder,
        string sourceFilePath,
        string targetFilePath,
        string tempFilePath,
        CancellationToken cancellationToken
    )
    {
        EnsureDirectoryExists(Path.GetDirectoryName(targetFilePath));

        if (string.Equals(sourceFilePath, targetFilePath, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(tempFilePath, sourceFilePath, true);
        }
        else
        {
            File.Move(tempFilePath, targetFilePath, true);

            if (File.Exists(sourceFilePath))
            {
                File.Delete(sourceFilePath);
            }

            await UpdateMemeOrderPathAsync(memeOrder, targetFilePath, cancellationToken);
        }

        await SyncDevelopmentCopyAsync(sourceFilePath, targetFilePath);
    }

    private async Task SyncDevelopmentCopyAsync(string sourceFilePath, string targetFilePath)
    {
        try
        {
            var mirroredSourcePath = TryGetMirroredRandomMemePath(sourceFilePath);
            var mirroredTargetPath = TryGetMirroredRandomMemePath(targetFilePath);

            if (!string.IsNullOrWhiteSpace(mirroredTargetPath) && File.Exists(targetFilePath))
            {
                EnsureDirectoryExists(Path.GetDirectoryName(mirroredTargetPath));
                File.Copy(targetFilePath, mirroredTargetPath, true);
                File.SetLastWriteTimeUtc(mirroredTargetPath, File.GetLastWriteTimeUtc(targetFilePath));

                if (
                    !string.IsNullOrWhiteSpace(mirroredSourcePath)
                    && !string.Equals(
                        mirroredSourcePath,
                        mirroredTargetPath,
                        StringComparison.OrdinalIgnoreCase
                    )
                    && File.Exists(mirroredSourcePath)
                )
                {
                    File.Delete(mirroredSourcePath);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Не удалось синхронизировать dev-копию медиафайла {FilePath}", targetFilePath);
        }

        await Task.CompletedTask;
    }

    private string? TryGetMirroredRandomMemePath(string filePath)
    {
        string? result = null;

        if (webHostEnvironment.IsProduction() && !string.IsNullOrWhiteSpace(filePath))
        {
            var primaryRoot = NormalizePath(
                Path.Combine(webHostEnvironment.WebRootPath, "Alerts", "random_meme")
            );
            var developmentRoot = TryGetDevelopmentRandomMemeRootPath();

            if (!string.IsNullOrWhiteSpace(developmentRoot))
            {
                var normalizedDevelopmentRoot = NormalizePath(developmentRoot);
                var normalizedFilePath = NormalizePath(filePath);

                if (normalizedFilePath.StartsWith(primaryRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = Path.GetRelativePath(primaryRoot, normalizedFilePath);
                    result = Path.Combine(normalizedDevelopmentRoot, relativePath);
                }
                else if (
                    normalizedFilePath.StartsWith(
                        normalizedDevelopmentRoot,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    var relativePath = Path.GetRelativePath(
                        normalizedDevelopmentRoot,
                        normalizedFilePath
                    );
                    result = Path.Combine(primaryRoot, relativePath);
                }
            }
        }

        return result;
    }

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
        var result = Path.GetFullPath(filePath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );

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

            using var process = new Process();
            process.StartInfo = BuildProcessStartInfo("ffmpeg");

            process.StartInfo.ArgumentList.Add("-y");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(sourceFilePath);
            process.StartInfo.ArgumentList.Add("-vn");
            process.StartInfo.ArgumentList.Add("-map_metadata");
            process.StartInfo.ArgumentList.Add("-1");
            process.StartInfo.ArgumentList.Add("-c:a");
            process.StartInfo.ArgumentList.Add("libmp3lame");
            process.StartInfo.ArgumentList.Add("-b:a");
            process.StartInfo.ArgumentList.Add($"{MinimumAudioBitrateKbps}k");
            process.StartInfo.ArgumentList.Add(outputFilePath);

            if (process.Start())
            {
                var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

                await process.WaitForExitAsync(cancellationToken);

                var standardError = await standardErrorTask;
                await standardOutputTask;

                if (process.ExitCode == 0 && File.Exists(outputFilePath))
                {
                    result = true;
                    File.SetLastWriteTimeUtc(outputFilePath, DateTime.UtcNow);
                }
                else
                {
                    logger.LogWarning(
                        "ffmpeg завершился с кодом {ExitCode} при конвертации аудио {FilePath}. stderr: {StandardError}",
                        process.ExitCode,
                        sourceFilePath,
                        standardError
                    );
                }
            }
            else
            {
                logger.LogWarning(
                    "Не удалось запустить ffmpeg для конвертации аудио {FilePath}",
                    sourceFilePath
                );
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

            using var process = new Process();
            process.StartInfo = BuildProcessStartInfo("ffmpeg");

            process.StartInfo.ArgumentList.Add("-y");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(sourceFilePath);
            process.StartInfo.ArgumentList.Add("-vf");
            process.StartInfo.ArgumentList.Add($"fps={VideoFrameRate}");
            process.StartInfo.ArgumentList.Add("-r");
            process.StartInfo.ArgumentList.Add(VideoFrameRate.ToString());
            process.StartInfo.ArgumentList.Add("-c:v");
            process.StartInfo.ArgumentList.Add("libx264");
            process.StartInfo.ArgumentList.Add("-pix_fmt");
            process.StartInfo.ArgumentList.Add("yuv420p");
            process.StartInfo.ArgumentList.Add("-preset");
            process.StartInfo.ArgumentList.Add("veryfast");
            process.StartInfo.ArgumentList.Add("-crf");
            process.StartInfo.ArgumentList.Add("20");
            process.StartInfo.ArgumentList.Add("-c:a");
            process.StartInfo.ArgumentList.Add("aac");
            process.StartInfo.ArgumentList.Add("-b:a");
            process.StartInfo.ArgumentList.Add($"{MinimumAudioBitrateKbps}k");
            process.StartInfo.ArgumentList.Add("-movflags");
            process.StartInfo.ArgumentList.Add("+faststart");
            process.StartInfo.ArgumentList.Add(outputFilePath);

            if (process.Start())
            {
                var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

                await process.WaitForExitAsync(cancellationToken);

                var standardError = await standardErrorTask;
                await standardOutputTask;

                if (process.ExitCode == 0 && File.Exists(outputFilePath))
                {
                    result = true;
                    File.SetLastWriteTimeUtc(outputFilePath, DateTime.UtcNow);
                }
                else
                {
                    logger.LogWarning(
                        "ffmpeg завершился с кодом {ExitCode} при конвертации видео {FilePath}. stderr: {StandardError}",
                        process.ExitCode,
                        sourceFilePath,
                        standardError
                    );
                }
            }
            else
            {
                logger.LogWarning(
                    "Не удалось запустить ffmpeg для конвертации видео {FilePath}",
                    sourceFilePath
                );
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
        var result = await ReadProbeWithFfprobeAsync(filePath, cancellationToken);

        if (
            result.BitrateKbps is null
            && result.AverageFrameRate is null
            && result.RawFrameRate is null
        )
        {
            result = await ReadProbeWithFfmpegAsync(filePath, cancellationToken);
        }

        return result;
    }

    private async Task<(
        long? BitrateKbps,
        double? AverageFrameRate,
        double? RawFrameRate
    )> ReadProbeWithFfprobeAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = (
            BitrateKbps: (long?)null,
            AverageFrameRate: (double?)null,
            RawFrameRate: (double?)null
        );

        try
        {
            using var process = new Process();
            process.StartInfo = BuildProcessStartInfo("ffprobe");

            process.StartInfo.ArgumentList.Add("-v");
            process.StartInfo.ArgumentList.Add("error");
            process.StartInfo.ArgumentList.Add("-print_format");
            process.StartInfo.ArgumentList.Add("json");
            process.StartInfo.ArgumentList.Add("-show_format");
            process.StartInfo.ArgumentList.Add("-show_streams");
            process.StartInfo.ArgumentList.Add(filePath);

            if (process.Start())
            {
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    using var document = JsonDocument.Parse(output);
                    var root = document.RootElement;

                    long? formatBitrateKbps = null;
                    if (
                        root.TryGetProperty("format", out var formatElement)
                        && formatElement.TryGetProperty("bit_rate", out var formatBitrateElement)
                    )
                    {
                        formatBitrateKbps = ParseKbps(formatBitrateElement);
                    }

                    if (root.TryGetProperty("streams", out var streamsElement))
                    {
                        foreach (var stream in streamsElement.EnumerateArray())
                        {
                            var codecType = stream.TryGetProperty(
                                "codec_type",
                                out var codecTypeElement
                            )
                                ? codecTypeElement.GetString()
                                : null;

                            if (codecType == "video")
                            {
                                var averageFrameRate = stream.TryGetProperty(
                                    "avg_frame_rate",
                                    out var avgFrameRateElement
                                )
                                    ? ParseFrameRate(avgFrameRateElement.GetString())
                                    : null;
                                var rawFrameRate = stream.TryGetProperty(
                                    "r_frame_rate",
                                    out var rawFrameRateElement
                                )
                                    ? ParseFrameRate(rawFrameRateElement.GetString())
                                    : null;
                                var videoBitrateKbps = stream.TryGetProperty(
                                    "bit_rate",
                                    out var streamBitrateElement
                                )
                                    ? ParseKbps(streamBitrateElement)
                                    : null;

                                result = (
                                    videoBitrateKbps ?? formatBitrateKbps,
                                    averageFrameRate,
                                    rawFrameRate
                                );
                                break;
                            }

                            if (codecType == "audio")
                            {
                                var audioBitrateKbps = stream.TryGetProperty(
                                    "bit_rate",
                                    out var streamBitrateElement
                                )
                                    ? ParseKbps(streamBitrateElement)
                                    : null;

                                result = (audioBitrateKbps ?? formatBitrateKbps, null, null);
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "ffprobe недоступен для файла {FilePath}", filePath);
        }

        return result;
    }

    private async Task<(
        long? BitrateKbps,
        double? AverageFrameRate,
        double? RawFrameRate
    )> ReadProbeWithFfmpegAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = (
            BitrateKbps: (long?)null,
            AverageFrameRate: (double?)null,
            RawFrameRate: (double?)null
        );

        try
        {
            using var process = new Process();
            process.StartInfo = BuildProcessStartInfo("ffmpeg");

            process.StartInfo.ArgumentList.Add("-hide_banner");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(filePath);

            if (process.Start())
            {
                var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
                await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                var bitrateMatch = System.Text.RegularExpressions.Regex.Match(
                    standardError,
                    @"bitrate:\s*(\d+(?:\.\d+)?)\s*kbits?/s",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                if (
                    bitrateMatch.Success
                    && double.TryParse(
                        bitrateMatch.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var bitrateKbps
                    )
                )
                {
                    result.BitrateKbps = (long)Math.Round(bitrateKbps);
                }

                var frameRateMatch = System.Text.RegularExpressions.Regex.Match(
                    standardError,
                    @"(?<rate>\d+(?:\.\d+)?)\s*fps",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                if (
                    frameRateMatch.Success
                    && double.TryParse(
                        frameRateMatch.Groups["rate"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var frameRate
                    )
                )
                {
                    result.AverageFrameRate = frameRate;
                    result.RawFrameRate = frameRate;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "ffmpeg не смог получить информацию о файле {FilePath}", filePath);
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
        var result = sourceFilePath;

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

        result = Path.Combine(cacheDirectory, hash + extension);
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

    private static long? ParseKbps(JsonElement element)
    {
        long? result = null;

        if (
            long.TryParse(element.GetString(), out var bitrateBitsPerSecond)
            && bitrateBitsPerSecond > 0
        )
        {
            result = bitrateBitsPerSecond / 1000;
        }

        return result;
    }

    private static double? ParseFrameRate(string? frameRate)
    {
        double? result = null;

        if (!string.IsNullOrWhiteSpace(frameRate))
        {
            if (frameRate.Contains('/'))
            {
                var parts = frameRate.Split('/');
                if (
                    parts.Length == 2
                    && double.TryParse(
                        parts[0],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var numerator
                    )
                    && double.TryParse(
                        parts[1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var denominator
                    )
                    && denominator != 0
                )
                {
                    result = numerator / denominator;
                }
            }
            else if (
                double.TryParse(
                    frameRate,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsedFrameRate
                )
            )
            {
                result = parsedFrameRate;
            }
        }

        return result;
    }

    private static ProcessStartInfo BuildProcessStartInfo(string fileName)
    {
        var result = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        return result;
    }

    private static void EnsureDirectoryExists(string? directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
