using FFMpegCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace MARS.Server.Services.Discord.Gateway;

public class MediaCompressor(ILogger<MediaCompressor> logger) : IMediaCompressor
{
    private const int MaxImageDimension = 1920;
    private static readonly int[] JpegQualityLadder = [85, 75, 60, 45];

    public async Task<Stream?> CompressImageAsync(
        Stream source,
        string fileName,
        long maxSize,
        CancellationToken ct
    )
    {
        var sourceStream = new MemoryStream();
        await source.CopyToAsync(sourceStream, ct);
        sourceStream.Position = 0;

        if (sourceStream.Length <= maxSize)
        {
            return sourceStream;
        }

        try
        {
            using var image = await Image.LoadAsync(sourceStream, ct);

            if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
            {
                image.Mutate(x =>
                    x.Resize(
                        new ResizeOptions
                        {
                            Size = new Size(MaxImageDimension, MaxImageDimension),
                            Mode = ResizeMode.Max,
                        }
                    )
                );
            }

            foreach (var quality in JpegQualityLadder)
            {
                var output = new MemoryStream();
                var encoder = new JpegEncoder { Quality = quality };
                await image.SaveAsync(output, encoder, ct);

                if (output.Length <= maxSize)
                {
                    output.Position = 0;
                    logger.LogInformation(
                        "Изображение {FileName} сжато до {Size}MB (JPEG quality={Quality})",
                        fileName,
                        output.Length / 1024.0 / 1024.0,
                        quality
                    );
                    return output;
                }

                await output.DisposeAsync();
            }

            logger.LogInformation(
                "Изображение {FileName} не удалось сжать до лимита {MaxSize}MB",
                fileName,
                maxSize / 1024 / 1024
            );
            return null;
        }
        catch (UnknownImageFormatException)
        {
            logger.LogInformation("Файл {FileName} не является изображением", fileName);
            return null;
        }
    }

    public async Task<IReadOnlyList<(Stream Stream, string FileName)>?> CompressVideoAsync(
        Stream source,
        string fileName,
        long maxSize,
        CancellationToken ct
    )
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mars_video_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputPath = Path.Combine(tempDir, $"input{Path.GetExtension(fileName)}");
            await using (var fs = File.Create(inputPath))
            {
                await source.CopyToAsync(fs, ct);
            }

            var compressedPath = Path.Combine(tempDir, "compressed.mp4");

            try
            {
                await FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(
                        compressedPath,
                        true,
                        options =>
                        {
                            options
                                .WithVideoCodec("libx264")
                                .WithConstantRateFactor(28)
                                .WithCustomArgument("-preset veryfast")
                                .WithCustomArgument(
                                    "-vf scale='min(1280,iw)':min(720,ih):force_original_aspect_ratio=decrease"
                                )
                                .WithCustomArgument("-r 30")
                                .WithAudioCodec("aac")
                                .WithAudioBitrate(128)
                                .WithFastStart();
                        }
                    )
                    .ProcessAsynchronously();
            }
            catch
            {
                logger.LogInformation("Не удалось сжать видео {FileName}", fileName);
                return null;
            }

            var compressedSize = new FileInfo(compressedPath).Length;

            if (compressedSize <= maxSize)
            {
                var stream = new MemoryStream(await File.ReadAllBytesAsync(compressedPath, ct));
                logger.LogInformation(
                    "Видео {FileName} сжато до {Size}MB",
                    fileName,
                    compressedSize / 1024.0 / 1024.0
                );
                return [(stream, fileName)];
            }

            var duration = await FFProbe.AnalyseAsync(compressedPath, cancellationToken: ct);

            var segmentCount = (int)Math.Ceiling((double)compressedSize / (maxSize * 0.85));
            var segmentTime = (int)(duration.Duration.TotalSeconds / segmentCount);
            if (segmentTime < 10)
            {
                segmentTime = 10;
            }

            var segmentsDir = Path.Combine(tempDir, "segments");
            Directory.CreateDirectory(segmentsDir);
            var outputPattern = Path.Combine(segmentsDir, "segment_%03d.mp4");

            try
            {
                await FFMpegArguments
                    .FromFileInput(compressedPath)
                    .OutputToFile(
                        outputPattern,
                        false,
                        options =>
                        {
                            options
                                .WithCustomArgument("-c copy")
                                .WithCustomArgument("-f segment")
                                .WithCustomArgument($"-segment_time {segmentTime}")
                                .WithCustomArgument("-reset_timestamps 1");
                        }
                    )
                    .ProcessAsynchronously();
            }
            catch
            {
                logger.LogInformation(
                    "Не удалось разрезать видео {FileName} на сегменты",
                    fileName
                );
                return null;
            }

            var segmentFiles = Directory
                .GetFiles(segmentsDir, "segment_*.mp4")
                .OrderBy(f => f)
                .ToList();

            if (segmentFiles.Count == 0)
            {
                return null;
            }

            var result = new List<(Stream Stream, string FileName)>();
            foreach (var segPath in segmentFiles)
            {
                var segSize = new FileInfo(segPath).Length;
                if (segSize > maxSize)
                {
                    foreach (var r in result)
                    {
                        await r.Stream.DisposeAsync();
                    }

                    logger.LogInformation("Сегмент видео {FileName} превышает лимит", fileName);
                    return null;
                }

                var stream = new MemoryStream(await File.ReadAllBytesAsync(segPath, ct));
                result.Add((stream, Path.GetFileName(segPath)));
            }

            logger.LogInformation(
                "Видео {FileName} разрезано на {Count} сегментов",
                fileName,
                result.Count
            );
            return result;
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    public async Task<Stream?> CompressAudioAsync(
        Stream source,
        string fileName,
        long maxSize,
        CancellationToken ct
    )
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mars_audio_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputPath = Path.Combine(tempDir, $"input{Path.GetExtension(fileName)}");
            await using (var fs = File.Create(inputPath))
            {
                await source.CopyToAsync(fs, ct);
            }

            var outputPath = Path.Combine(tempDir, "output.mp3");

            try
            {
                await FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(
                        outputPath,
                        true,
                        options =>
                        {
                            options
                                .WithAudioCodec("libmp3lame")
                                .WithAudioBitrate(96)
                                .WithCustomArgument("-ac 1");
                        }
                    )
                    .ProcessAsynchronously();
            }
            catch
            {
                logger.LogInformation("Не удалось сжать аудио {FileName}", fileName);
                return null;
            }

            var outputSize = new FileInfo(outputPath).Length;
            if (outputSize > maxSize)
            {
                logger.LogInformation(
                    "Аудио {FileName} не удалось сжать до лимита {MaxSize}MB (получено {Size}MB)",
                    fileName,
                    maxSize / 1024 / 1024,
                    outputSize / 1024.0 / 1024.0
                );
                return null;
            }

            var stream = new MemoryStream(await File.ReadAllBytesAsync(outputPath, ct));
            logger.LogInformation(
                "Аудио {FileName} сжато до {Size}MB",
                fileName,
                outputSize / 1024.0 / 1024.0
            );
            return stream;
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }
}
