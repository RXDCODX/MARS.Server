using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Media;

public class FfprobeMediaInspector : IMediaInspector
{
    private readonly ILogger<FfprobeMediaInspector> _logger;

    public FfprobeMediaInspector(ILogger<FfprobeMediaInspector> logger)
    {
        _logger = logger;
    }

    public async Task<(long? BitrateKbps, double? AverageFrameRate, double? RawFrameRate)> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = await ReadProbeWithFfprobeAsync(filePath, cancellationToken);

        if (result.BitrateKbps is null && result.AverageFrameRate is null && result.RawFrameRate is null)
        {
            result = await ReadProbeWithFfmpegAsync(filePath, cancellationToken);
        }

        return result;
    }

    private async Task<(long? BitrateKbps, double? AverageFrameRate, double? RawFrameRate)> ReadProbeWithFfprobeAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = (BitrateKbps: (long?)null, AverageFrameRate: (double?)null, RawFrameRate: (double?)null);

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
                    if (root.TryGetProperty("format", out var formatElement) && formatElement.TryGetProperty("bit_rate", out var formatBitrateElement))
                    {
                        formatBitrateKbps = ParseKbps(formatBitrateElement);
                    }

                    if (root.TryGetProperty("streams", out var streamsElement))
                    {
                        foreach (var stream in streamsElement.EnumerateArray())
                        {
                            var codecType = stream.TryGetProperty("codec_type", out var codecTypeElement) ? codecTypeElement.GetString() : null;

                            if (codecType == "video")
                            {
                                var averageFrameRate = stream.TryGetProperty("avg_frame_rate", out var avgFrameRateElement) ? ParseFrameRate(avgFrameRateElement.GetString()) : null;
                                var rawFrameRate = stream.TryGetProperty("r_frame_rate", out var rawFrameRateElement) ? ParseFrameRate(rawFrameRateElement.GetString()) : null;
                                var videoBitrateKbps = stream.TryGetProperty("bit_rate", out var streamBitrateElement) ? ParseKbps(streamBitrateElement) : null;

                                result = (videoBitrateKbps ?? formatBitrateKbps, averageFrameRate, rawFrameRate);
                                break;
                            }

                            if (codecType == "audio")
                            {
                                var audioBitrateKbps = stream.TryGetProperty("bit_rate", out var streamBitrateElement) ? ParseKbps(streamBitrateElement) : null;

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
            _logger.LogDebug(ex, "ffprobe недоступен для файла {FilePath}", filePath);
        }

        return result;
    }

    private async Task<(long? BitrateKbps, double? AverageFrameRate, double? RawFrameRate)> ReadProbeWithFfmpegAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = (BitrateKbps: (long?)null, AverageFrameRate: (double?)null, RawFrameRate: (double?)null);

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

                var bitrateMatch = System.Text.RegularExpressions.Regex.Match(standardError, @"bitrate:\s*(\d+(?:\.\d+)?)\s*kbits?/s", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (bitrateMatch.Success && double.TryParse(bitrateMatch.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bitrateKbps))
                {
                    result.BitrateKbps = (long)Math.Round(bitrateKbps);
                }

                var frameRateMatch = System.Text.RegularExpressions.Regex.Match(standardError, @"(?<rate>\d+(?:\.\d+)?)\s*fps", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (frameRateMatch.Success && double.TryParse(frameRateMatch.Groups["rate"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var frameRate))
                {
                    result.AverageFrameRate = frameRate;
                    result.RawFrameRate = frameRate;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ffmpeg не смог получить информацию о файле {FilePath}", filePath);
        }

        return result;
    }

    private static long? ParseKbps(JsonElement element)
    {
        long? result = null;

        if (long.TryParse(element.GetString(), out var bitrateBitsPerSecond) && bitrateBitsPerSecond > 0)
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
                if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var numerator) && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var denominator) && denominator != 0)
                {
                    result = numerator / denominator;
                }
            }
            else if (double.TryParse(frameRate, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedFrameRate))
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
}
