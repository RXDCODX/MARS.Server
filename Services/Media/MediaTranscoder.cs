using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using MARS.Server.Exstensions;
using MARS.Server.Services.PyroAlerts.Entitys;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Media;

public class MediaTranscoder : IMediaTranscoder
{
    private const int MinimumAudioBitrateKbps = 128;
    private const int MinimumVideoBitrateKbps = 128;
    private const int VideoFrameRate = 30;
    private const string CacheFolderName = "_converted";

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MediaTranscoder> _logger;
    private readonly IMediaInspector _inspector;
    private readonly IMediaFileStorageService _storage;

    public MediaTranscoder(IWebHostEnvironment env, ILogger<MediaTranscoder> logger, IMediaInspector inspector, IMediaFileStorageService storage)
    {
        _env = env;
        _logger = logger;
        _inspector = inspector;
        _storage = storage;
    }

    public async Task<string> EnsurePlayableAsync(string sourceFullPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourceFullPath) || !File.Exists(sourceFullPath)) return sourceFullPath;

            var extension = Path.GetExtension(sourceFullPath);
            var mediaType = await extension.GetFileMediaTypeAsync();

            if (mediaType != MediaType.Audio && mediaType != MediaType.Video)
                return sourceFullPath;

            var probe = await _inspector.ProbeAsync(sourceFullPath, cancellationToken);
            var needs = NeedsTranscoding(mediaType, probe);

            if (!needs)
            {
                return sourceFullPath;
            }

            var targetFilePath = GetTargetFilePath(sourceFullPath, mediaType);

            if (IsTranscodedVersionReady(sourceFullPath, targetFilePath))
            {
                await SyncDevelopmentCopyAsync(sourceFullPath, targetFilePath);
                return targetFilePath;
            }

            _logger.LogInformation(GetConversionMessage(sourceFullPath, mediaType, probe.BitrateKbps));

            var tempFile = GetCacheTempFilePath(sourceFullPath, mediaType);
            var transcodeSucceeded = mediaType == MediaType.Audio
                ? await ConvertAudioAsync(sourceFullPath, tempFile, cancellationToken)
                : await ConvertVideoAsync(sourceFullPath, tempFile, cancellationToken);

            if (!transcodeSucceeded) return sourceFullPath;

            EnsureDirectoryExists(Path.GetDirectoryName(targetFilePath));

            if (string.Equals(sourceFullPath, targetFilePath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(tempFile, sourceFullPath, true);
            }
            else
            {
                File.Move(tempFile, targetFilePath, true);
                if (File.Exists(sourceFullPath)) File.Delete(sourceFullPath);
            }

            File.SetLastWriteTimeUtc(targetFilePath, DateTime.UtcNow);

            await SyncDevelopmentCopyAsync(sourceFullPath, targetFilePath);

            return targetFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transcode failed for {File}", sourceFullPath);
            return sourceFullPath;
        }
    }

    private static bool IsTranscodedVersionReady(string sourceFilePath, string targetFilePath)
    {
        return File.Exists(targetFilePath) && File.GetLastWriteTimeUtc(targetFilePath) >= File.GetLastWriteTimeUtc(sourceFilePath);
    }

    private static string GetTargetFilePath(string sourceFilePath, MediaType mediaType)
    {
        return mediaType == MediaType.Audio ? Path.ChangeExtension(sourceFilePath, ".mp3") : Path.ChangeExtension(sourceFilePath, ".mp4");
    }

    private string GetCacheTempFilePath(string sourceFilePath, MediaType mediaType)
    {
        var cacheDirectory = Path.Combine(_env.WebRootPath, "Alerts", CacheFolderName);
        Directory.CreateDirectory(cacheDirectory);

        var key = string.Join('|', sourceFilePath, File.GetLastWriteTimeUtc(sourceFilePath).Ticks, mediaType, MinimumAudioBitrateKbps, MinimumVideoBitrateKbps, VideoFrameRate);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        var extension = mediaType == MediaType.Audio ? ".mp3" : ".mp4";

        return Path.Combine(cacheDirectory, hash + extension);
    }

    private static string GetConversionMessage(string sourceFilePath, MediaType mediaType, long? bitrateKbps)
    {
        var fileName = Path.GetFileName(sourceFilePath);
        var bitrateText = bitrateKbps is > 0 ? $"{bitrateKbps} kbps" : "unknown bitrate";
        var mediaKindText = mediaType == MediaType.Audio ? "аудио" : "видео";
        var result = $"Перекодирую {mediaKindText} {fileName} ({bitrateText}) для Chrome";
        if (mediaType == MediaType.Video) result += $" и фиксирую {VideoFrameRate} fps";
        return result;
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

                if (!string.IsNullOrWhiteSpace(mirroredSourcePath) && !string.Equals(mirroredSourcePath, mirroredTargetPath, StringComparison.OrdinalIgnoreCase) && File.Exists(mirroredSourcePath))
                {
                    File.Delete(mirroredSourcePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось синхронизировать dev-копию медиафайла {FilePath}", targetFilePath);
        }

        await Task.CompletedTask;
    }

    private static bool NeedsTranscoding(MediaType mediaType, (long? BitrateKbps, double? AverageFrameRate, double? RawFrameRate) probe)
    {
        if (mediaType == MediaType.Audio)
        {
            return probe.BitrateKbps is null || probe.BitrateKbps < MinimumAudioBitrateKbps;
        }

        if (mediaType == MediaType.Video)
        {
            var hasLowBitrate = probe.BitrateKbps is null || probe.BitrateKbps < MinimumVideoBitrateKbps;
            var hasVariableFrameRate = IsVariableFrameRate(probe.AverageFrameRate, probe.RawFrameRate);
            return hasLowBitrate || hasVariableFrameRate;
        }

        return false;
    }

    private static bool IsVariableFrameRate(double? averageFrameRate, double? rawFrameRate)
    {
        if (averageFrameRate is > 0 && rawFrameRate is > 0)
        {
            return Math.Abs(averageFrameRate.Value - rawFrameRate.Value) > 0.01;
        }

        return false;
    }

    private async Task<bool> ConvertAudioAsync(string sourceFilePath, string outputFilePath, CancellationToken cancellationToken)
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
                    options => options
                        .WithAudioCodec("libmp3lame")
                        .WithAudioBitrate(MinimumAudioBitrateKbps)
                        .WithCustomArgument("-vn")
                        .WithCustomArgument("-map_metadata -1")
                )
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();

            if (File.Exists(outputFilePath))
            {
                File.SetLastWriteTimeUtc(outputFilePath, DateTime.UtcNow);
                result = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка конвертации аудио {FilePath}", sourceFilePath);
        }

        return result;
    }

    private async Task<bool> ConvertVideoAsync(string sourceFilePath, string outputFilePath, CancellationToken cancellationToken)
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
                    options => options
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
                File.SetLastWriteTimeUtc(outputFilePath, DateTime.UtcNow);
                result = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка конвертации видео {FilePath}", sourceFilePath);
        }

        return result;
    }

    private static void EnsureDirectoryExists(string? directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath)) Directory.CreateDirectory(directoryPath);
    }

    private static string? TryGetMirroredRandomMemePath(string filePath)
    {
        // mirror logic copied from TwitchMediaPreparationService: only active when running in production context mapping between primary/development roots
        // For uploaded files (dev) this will typically return development mirror path; keep simple here: return null to avoid cross-process mirroring.
        return null;
    }
}
