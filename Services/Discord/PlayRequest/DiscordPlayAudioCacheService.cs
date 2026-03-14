using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.YouTube;

namespace MARS.Server.Services.Discord.PlayRequest;

public class DiscordPlayAudioCacheService(
    YouTubeResolver youTubeResolver,
    ILogger<DiscordPlayAudioCacheService> logger
)
{
    public const long DefaultMaxAttachmentSizeBytes = 8 * 1024 * 1024;

    private const int MaxBitrateKbps = 192;
    private const int MinBitrateKbps = 32;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);
    private static readonly int[] StandardBitratesKbps = [192, 160, 128, 96, 80, 64, 48, 32];
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _cacheLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly string _cacheDirectoryPath = Path.Combine(
        Path.GetTempPath(),
        "mars-discord-play-cache"
    );

    public async Task<OperationResult<DiscordPreparedAudioFile>> PrepareAudioAsync(
        BaseTrackInfo track,
        long maxAttachmentSizeBytes,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<DiscordPreparedAudioFile>.Bad(
            "Не удалось подготовить аудиофайл"
        );

        CleanupExpiredCacheFiles();

        if (track is not null && maxAttachmentSizeBytes > 0)
        {
            var videoId = youTubeResolver.GetVideoId(track);
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                var cacheLock = _cacheLocks.GetOrAdd(videoId, _ => new SemaphoreSlim(1, 1));
                await cacheLock.WaitAsync(cancellationToken);

                try
                {
                    var cachedFile = TryGetCachedFile(videoId, maxAttachmentSizeBytes);
                    if (cachedFile is not null)
                    {
                        result = OperationResult<DiscordPreparedAudioFile>.Ok(
                            "Аудиофайл взят из кэша",
                            cachedFile
                        );
                    }
                    else
                    {
                        result = await PrepareAndCacheAudioAsync(
                            track,
                            videoId,
                            maxAttachmentSizeBytes,
                            cancellationToken
                        );
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Ошибка подготовки Discord audio cache для {VideoId}",
                        videoId
                    );
                    result = OperationResult<DiscordPreparedAudioFile>.Bad(
                        $"Ошибка подготовки аудио: {ex.Message}"
                    );
                }
                finally
                {
                    cacheLock.Release();
                }
            }
            else
            {
                result = OperationResult<DiscordPreparedAudioFile>.Bad(
                    "Не удалось определить YouTube video id"
                );
            }
        }
        else
        {
            result = OperationResult<DiscordPreparedAudioFile>.Bad(
                "Неверные параметры подготовки аудиофайла"
            );
        }

        return result;
    }

    private async Task<OperationResult<DiscordPreparedAudioFile>> PrepareAndCacheAudioAsync(
        BaseTrackInfo track,
        string videoId,
        long maxAttachmentSizeBytes,
        CancellationToken cancellationToken
    )
    {
        var result = OperationResult<DiscordPreparedAudioFile>.Bad(
            "Не удалось подготовить аудиофайл"
        );
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "mars-discord-play-temp",
            Guid.NewGuid().ToString("N")
        );

        try
        {
            Directory.CreateDirectory(_cacheDirectoryPath);
            Directory.CreateDirectory(tempDirectory);

            var sourceFilePath = await youTubeResolver.DownloadBestAudioStreamAsync(
                track,
                tempDirectory,
                cancellationToken
            );

            if (!string.IsNullOrWhiteSpace(sourceFilePath) && File.Exists(sourceFilePath))
            {
                var bitrates = BuildBitrateCandidates(track.Duration, maxAttachmentSizeBytes);

                foreach (var bitrate in bitrates)
                {
                    var convertedFilePath = Path.Combine(
                        tempDirectory,
                        string.Concat(videoId, "_", bitrate, "kbps.mp3")
                    );

                    var convertResult = await ConvertToMp3Async(
                        sourceFilePath,
                        convertedFilePath,
                        bitrate,
                        cancellationToken
                    );

                    if (convertResult && File.Exists(convertedFilePath))
                    {
                        var convertedFileInfo = new FileInfo(convertedFilePath);
                        if (convertedFileInfo.Length <= maxAttachmentSizeBytes)
                        {
                            var cachedFilePath = Path.Combine(
                                _cacheDirectoryPath,
                                Path.GetFileName(convertedFilePath)
                            );

                            File.Copy(convertedFilePath, cachedFilePath, true);
                            result = OperationResult<DiscordPreparedAudioFile>.Ok(
                                "Аудиофайл подготовлен",
                                CreatePreparedAudioFile(cachedFilePath, false, bitrate)
                            );
                            break;
                        }
                    }
                }

                if (!result.Success)
                {
                    var sourceFileInfo = new FileInfo(sourceFilePath);
                    if (sourceFileInfo.Length <= maxAttachmentSizeBytes)
                    {
                        var cachedSourceFilePath = Path.Combine(
                            _cacheDirectoryPath,
                            string.Concat(videoId, "_source", sourceFileInfo.Extension)
                        );

                        File.Copy(sourceFilePath, cachedSourceFilePath, true);
                        result = OperationResult<DiscordPreparedAudioFile>.Ok(
                            "Аудиофайл подготовлен в исходном контейнере",
                            CreatePreparedAudioFile(cachedSourceFilePath, false, 0)
                        );
                    }
                    else
                    {
                        result = OperationResult<DiscordPreparedAudioFile>.Bad(
                            string.Concat(
                                "Выбранный трек не помещается в лимит Discord ",
                                FormatFileSize(maxAttachmentSizeBytes),
                                " даже после сжатия."
                            )
                        );
                    }
                }
            }
            else
            {
                result = OperationResult<DiscordPreparedAudioFile>.Bad(
                    "Не удалось скачать исходный аудиопоток"
                );
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }

        return result;
    }

    private DiscordPreparedAudioFile? TryGetCachedFile(string videoId, long maxAttachmentSizeBytes)
    {
        DiscordPreparedAudioFile? result = null;

        Directory.CreateDirectory(_cacheDirectoryPath);

        var cachedFiles = Directory
            .GetFiles(_cacheDirectoryPath, string.Concat(videoId, "_*.*"))
            .Select(path => new FileInfo(path))
            .Where(fileInfo =>
                fileInfo.Exists
                && DateTime.UtcNow - fileInfo.LastWriteTimeUtc <= CacheLifetime
                && fileInfo.Length <= maxAttachmentSizeBytes
            )
            .OrderByDescending(fileInfo => fileInfo.Length)
            .ToArray();

        if (cachedFiles.Length > 0)
        {
            var cachedFile = cachedFiles[0];
            result = CreatePreparedAudioFile(
                cachedFile.FullName,
                true,
                ExtractBitrateFromFileName(cachedFile.Name)
            );
        }

        return result;
    }

    private async Task<bool> ConvertToMp3Async(
        string sourceFilePath,
        string outputFilePath,
        int bitrateKbps,
        CancellationToken cancellationToken
    )
    {
        var result = false;

        try
        {
            using var process = new Process();

            process.StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            process.StartInfo.ArgumentList.Add("-y");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(sourceFilePath);
            process.StartInfo.ArgumentList.Add("-vn");
            process.StartInfo.ArgumentList.Add("-map_metadata");
            process.StartInfo.ArgumentList.Add("-1");
            process.StartInfo.ArgumentList.Add("-acodec");
            process.StartInfo.ArgumentList.Add("libmp3lame");
            process.StartInfo.ArgumentList.Add("-b:a");
            process.StartInfo.ArgumentList.Add(string.Concat(bitrateKbps, "k"));
            process.StartInfo.ArgumentList.Add(outputFilePath);

            if (process.Start())
            {
                var standardErrorTask = process.StandardError.ReadToEndAsync();
                var standardOutputTask = process.StandardOutput.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                var standardError = await standardErrorTask;
                await standardOutputTask;

                if (process.ExitCode == 0 && File.Exists(outputFilePath))
                {
                    result = true;
                }
                else
                {
                    logger.LogWarning(
                        "ffmpeg завершился с кодом {ExitCode} при битрейте {BitrateKbps} kbps. stderr: {StandardError}",
                        process.ExitCode,
                        bitrateKbps,
                        standardError
                    );
                }
            }
            else
            {
                logger.LogWarning("Не удалось запустить ffmpeg для подготовки Discord play аудио.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Ошибка конвертации Discord play аудио в mp3 с битрейтом {BitrateKbps} kbps",
                bitrateKbps
            );
        }

        return result;
    }

    private void CleanupExpiredCacheFiles()
    {
        if (Directory.Exists(_cacheDirectoryPath))
        {
            var cachedFiles = Directory.GetFiles(_cacheDirectoryPath);

            foreach (var cachedFilePath in cachedFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(cachedFilePath);
                    if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > CacheLifetime)
                    {
                        fileInfo.Delete();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Не удалось очистить файл Discord audio cache {CachedFilePath}",
                        cachedFilePath
                    );
                }
            }
        }
    }

    private static IReadOnlyList<int> BuildBitrateCandidates(
        TimeSpan duration,
        long maxAttachmentSizeBytes
    )
    {
        var result = new List<int>();
        var maxAllowedBitrate = CalculateTargetBitrateKbps(duration, maxAttachmentSizeBytes);

        foreach (var standardBitrate in StandardBitratesKbps)
        {
            if (standardBitrate <= maxAllowedBitrate && !result.Contains(standardBitrate))
            {
                result.Add(standardBitrate);
            }
        }

        if (result.Count == 0)
        {
            result.Add(MinBitrateKbps);
        }

        return result;
    }

    private static int CalculateTargetBitrateKbps(TimeSpan duration, long maxAttachmentSizeBytes)
    {
        var result = MaxBitrateKbps;

        if (duration > TimeSpan.FromSeconds(1) && maxAttachmentSizeBytes > 0)
        {
            var safeBudgetBytes = Math.Max(maxAttachmentSizeBytes - 128 * 1024, 256 * 1024);
            var bitsPerSecond = Math.Floor((safeBudgetBytes * 8D) / duration.TotalSeconds);
            var kilobitsPerSecond = (int)Math.Floor(bitsPerSecond / 1000D);

            result = Math.Clamp(kilobitsPerSecond, MinBitrateKbps, MaxBitrateKbps);
        }

        return result;
    }

    private static int ExtractBitrateFromFileName(string fileName)
    {
        var result = 0;
        var match = Regex.Match(fileName, @"_(\d+)kbps", RegexOptions.IgnoreCase);

        if (match.Success && int.TryParse(match.Groups[1].Value, out var bitrate))
        {
            result = bitrate;
        }

        return result;
    }

    private static DiscordPreparedAudioFile CreatePreparedAudioFile(
        string filePath,
        bool isFromCache,
        int bitrateKbps
    )
    {
        var fileInfo = new FileInfo(filePath);
        var result = new DiscordPreparedAudioFile
        {
            FilePath = fileInfo.FullName,
            FileName = fileInfo.Name,
            FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            IsFromCache = isFromCache,
            BitrateKbps = bitrateKbps,
        };

        return result;
    }

    private static string FormatFileSize(long bytes)
    {
        string result;

        if (bytes >= 1024 * 1024)
        {
            result = string.Concat(Math.Round(bytes / 1024D / 1024D, 1), " MB");
        }
        else if (bytes >= 1024)
        {
            result = string.Concat(Math.Round(bytes / 1024D, 1), " KB");
        }
        else
        {
            result = string.Concat(bytes, " B");
        }

        return result;
    }

    private void TryDeleteDirectory(string directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            try
            {
                Directory.Delete(directoryPath, true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось удалить временную директорию {DirectoryPath}", directoryPath);
            }
        }
    }
}