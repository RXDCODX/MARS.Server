using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.StreamAcrhive_UNUSED.Interfaces;
using MARS.Server.Services.StreamAcrhive_UNUSED.Models;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace MARS.Server.Services.StreamAcrhive_UNUSED;

public class FFmpegService(ILogger<FFmpegService> logger) : IFFmpegService
{
    private readonly string _ffmpegPath = FindFFmpegPath();

    // Пытаемся найти FFmpeg в системе

    public async Task<List<string>> SplitVideoFileAsync(
        string inputPath,
        string outputDirectory,
        long maxChunkSizeBytes,
        CancellationToken cancellationToken = default
    )
    {
        List<string> result = [];

        if (
            !string.IsNullOrWhiteSpace(inputPath)
            && !string.IsNullOrWhiteSpace(outputDirectory)
            && maxChunkSizeBytes > 0
        )
        {
            try
            {
                if (await IsFFmpegAvailableAsync(cancellationToken))
                {
                    // Получаем информацию о видео
                    var videoInfo = await GetVideoInfoAsync(inputPath, cancellationToken);

                    if (videoInfo != null)
                    {
                        // Вычисляем длительность каждой части в секундах
                        var totalDuration = videoInfo.Duration.TotalSeconds;
                        var chunkDurationSeconds =
                            (double)maxChunkSizeBytes / (videoInfo.Bitrate / 8.0);
                        var totalChunks = (int)Math.Ceiling(totalDuration / chunkDurationSeconds);

                        logger.LogInformation(
                            "Разбивка файла {FilePath} на {ChunkCount} частей по {ChunkDuration} секунд",
                            inputPath,
                            totalChunks,
                            chunkDurationSeconds
                        );

                        var baseFileName = Path.GetFileNameWithoutExtension(inputPath);
                        var extension = Path.GetExtension(inputPath);

                        for (var i = 0; i < totalChunks; i++)
                        {
                            var startTime = i * chunkDurationSeconds;
                            var endTime = Math.Min((i + 1) * chunkDurationSeconds, totalDuration);
                            var chunkFileName =
                                $"{baseFileName}_part_{i + 1}_of_{totalChunks}{extension}";
                            var outputPath = Path.Combine(outputDirectory, chunkFileName);

                            await CreateVideoChunkAsync(
                                inputPath,
                                outputPath,
                                startTime,
                                endTime - startTime,
                                cancellationToken
                            );
                            result.Add(outputPath);

                            logger.LogDebug(
                                "Создана часть {PartNumber} из {TotalParts}: {OutputPath}",
                                i + 1,
                                totalChunks,
                                outputPath
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при разбивке файла {FilePath}", inputPath);
                throw;
            }
        }

        return result;
    }

    public async Task<VideoInfo?> GetVideoInfoAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        VideoInfo? result = null;

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                var arguments =
                    $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath.Replace("ffmpeg", "ffprobe"),
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode == 0)
                {
                    var probeData = JsonSerializer.Deserialize<FFprobeOutput>(output);
                    if (
                        probeData?.Format is not null
                        && probeData.Streams is not null
                        && probeData.Streams.Count > 0
                    )
                    {
                        var videoStream = probeData.Streams.FirstOrDefault(s =>
                            s.CodecType == "video"
                        );
                        if (videoStream is not null)
                        {
                            result = new VideoInfo
                            {
                                Duration = TimeSpan.FromSeconds(
                                    double.Parse(probeData.Format.Duration ?? "0")
                                ),
                                Width = videoStream.Width,
                                Height = videoStream.Height,
                                Codec = videoStream.CodecName,
                                Bitrate = long.TryParse(probeData.Format.BitRate, out var bitrate)
                                    ? bitrate
                                    : 0,
                                FrameRate = videoStream.RFrameRate is not null
                                    ? ParseFrameRate(videoStream.RFrameRate)
                                    : 0,
                            };
                        }
                    }
                }
                else
                {
                    logger.LogError(
                        "FFprobe завершился с кодом {ExitCode} для файла {FilePath}",
                        process.ExitCode,
                        filePath
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при получении информации о видео {FilePath}", filePath);
            }
        }

        return result;
    }

    public async Task<bool> IsFFmpegAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = false;

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                logger.LogInformation("FFmpeg доступен по пути: {Path}", _ffmpegPath);
                result = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FFmpeg не найден или недоступен");
        }

        return result;
    }

    private async Task CreateVideoChunkAsync(
        string inputPath,
        string outputPath,
        double startTime,
        double duration,
        CancellationToken cancellationToken
    )
    {
        var arguments =
            $"-i \"{inputPath}\" -ss {startTime:F2} -t {duration:F2} -c copy \"{outputPath}\"";

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"FFmpeg завершился с ошибкой: {error}");
        }
    }

    private static string FindFFmpegPath()
    {
        // Список возможных путей к FFmpeg
        var possiblePaths = new[]
        {
            "ffmpeg", // В PATH
            "C:\\ffmpeg\\bin\\ffmpeg.exe",
            "C:\\Program Files\\ffmpeg\\bin\\ffmpeg.exe",
            "C:\\Program Files (x86)\\ffmpeg\\bin\\ffmpeg.exe",
            "/usr/bin/ffmpeg",
            "/usr/local/bin/ffmpeg",
        };

        foreach (var path in possiblePaths)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                process.Start();
                process.WaitForExit(5000); // Ждем максимум 5 секунд

                if (process.ExitCode == 0)
                {
                    return path;
                }
            }
            catch
            {
                // Продолжаем поиск
            }
        }

        // Возвращаем "ffmpeg" как fallback
        return "ffmpeg";
    }

    private static double ParseFrameRate(string frameRate)
    {
        try
        {
            if (frameRate.Contains('/'))
            {
                var parts = frameRate.Split('/');
                if (
                    parts.Length == 2
                    && double.TryParse(parts[0], out var numerator)
                    && double.TryParse(parts[1], out var denominator)
                    && denominator != 0
                )
                {
                    return numerator / denominator;
                }
            }
            else if (double.TryParse(frameRate, out var rate))
            {
                return rate;
            }
        }
        catch
        {
            // Игнорируем ошибки парсинга
        }

        return 0;
    }
}
