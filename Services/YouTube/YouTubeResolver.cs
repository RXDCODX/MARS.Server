using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MARS.Server.Services.SoundRequest.Entities;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace MARS.Server.Services.YouTube;

public class YouTubeResolver(ILogger<YouTubeResolver> logger)
{
    private readonly YoutubeClient _youtubeClient = new();

    public async Task<BaseTrackInfo?> ResolveQueryAsync(string query, CancellationToken ct)
    {
        BaseTrackInfo? result = null;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var tracks = await SearchTracksAsync(query, 1, ct);
            if (tracks.Length > 0)
            {
                result = tracks[0];
            }
        }

        return result;
    }

    public async Task<BaseTrackInfo[]> SearchTracksAsync(
        string query,
        int maxResults,
        CancellationToken ct
    )
    {
        BaseTrackInfo[] result = [];

        if (!string.IsNullOrWhiteSpace(query) && maxResults > 0)
        {
            try
            {
                var tracks = new List<BaseTrackInfo>(maxResults);
                var searchResults = _youtubeClient.Search.GetVideosAsync(query, ct);

                await foreach (var video in searchResults)
                {
                    var thumbnailUrl = video
                        .Thumbnails.OrderByDescending(t => t.Resolution.Area)
                        .FirstOrDefault()
                        ?.Url;

                    tracks.Add(
                        CreateTrackInfo(
                            video.Url,
                            video.Id,
                            video.Title,
                            video.Author.ChannelTitle,
                            video.Duration ?? TimeSpan.Zero,
                            thumbnailUrl
                        )
                    );

                    if (tracks.Count >= maxResults)
                    {
                        break;
                    }
                }

                result = [.. tracks];
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        return result;
    }

    public async Task<BaseTrackInfo?> ResolveVideoAsync(string url, CancellationToken ct)
    {
        BaseTrackInfo? result = null;

        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                var video = await _youtubeClient.Videos.GetAsync(url, ct);

                var thumbnailUrl = video
                    .Thumbnails.OrderByDescending(t => t.Resolution.Area)
                    .FirstOrDefault()
                    ?.Url;

                result = CreateTrackInfo(
                    url,
                    video.Id,
                    video.Title,
                    video.Author.ChannelTitle,
                    video.Duration ?? TimeSpan.Zero,
                    thumbnailUrl
                );
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        return result;
    }

    public async Task<BaseTrackInfo[]?> ResolvePlaylistAsync(string playlistUrl)
    {
        BaseTrackInfo[]? result = null;

        if (!string.IsNullOrWhiteSpace(playlistUrl))
        {
            try
            {
                var playlistVideos = _youtubeClient.Playlists.GetVideosAsync(playlistUrl);
                var videos = new List<BaseTrackInfo>();
                var count = 0;

                await foreach (var video in playlistVideos)
                {
                    if (count >= 200)
                    {
                        break;
                    }

                    var thumbnailUrl = video
                        .Thumbnails.OrderByDescending(t => t.Resolution.Area)
                        .FirstOrDefault()
                        ?.Url;

                    videos.Add(
                        CreateTrackInfo(
                            video.Url,
                            video.Id,
                            video.Title,
                            video.Author.ChannelTitle,
                            video.Duration ?? TimeSpan.Zero,
                            thumbnailUrl
                        )
                    );

                    count++;
                }

                result = [.. videos];
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
                result = [];
            }
        }

        return result;
    }

    public async Task<string?> DownloadBestAudioStreamAsync(
        BaseTrackInfo? track,
        string outputDirectory,
        CancellationToken ct
    )
    {
        string? result = null;

        if (track is not null && !string.IsNullOrWhiteSpace(outputDirectory))
        {
            var videoId = await ResolveDownloadVideoIdAsync(track, ct);
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                Directory.CreateDirectory(outputDirectory);

                result = await TryDownloadWithYoutubeReExplodeAsync(
                    track,
                    videoId,
                    outputDirectory,
                    ct
                );

                if (string.IsNullOrWhiteSpace(result))
                {
                    result = await TryDownloadWithYtDlpAsync(track, videoId, outputDirectory, ct);
                }

                if (string.IsNullOrWhiteSpace(result))
                {
                    logger.LogWarning(
                        "[YouTubeResolver] Не удалось скачать аудио для videoId={VideoId}. URL={Url}",
                        videoId,
                        track.Url
                    );
                }
            }
            else
            {
                logger.LogWarning(
                    "[YouTubeResolver] Не удалось определить video id для URL={Url}",
                    track.Url
                );
            }
        }

        return result;
    }

    public string? GetVideoId(BaseTrackInfo track)
    {
        string? result = null;

        if (track is not null)
        {
            if (!string.IsNullOrWhiteSpace(track.VideoId))
            {
                result = track.VideoId;
            }
            else if (track.Url is not null)
            {
                result = TryExtractVideoId(track.Url.ToString());
            }
        }

        return result;
    }

    private async Task<string?> ResolveDownloadVideoIdAsync(
        BaseTrackInfo track,
        CancellationToken ct
    )
    {
        var result = GetVideoId(track);

        if (track.Url is not null)
        {
            try
            {
                var video = await _youtubeClient.Videos.GetAsync(track.Url.ToString(), ct);
                if (!string.IsNullOrWhiteSpace(video.Id))
                {
                    result = video.Id;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "[YouTubeResolver] Не удалось обновить video id через Videos.GetAsync для URL={Url}",
                    track.Url
                );
            }
        }

        return result;
    }

    private async Task<string?> TryDownloadWithYoutubeReExplodeAsync(
        BaseTrackInfo track,
        string videoId,
        string outputDirectory,
        CancellationToken ct
    )
    {
        string? result = null;

        for (var attempt = 1; attempt <= 2 && string.IsNullOrWhiteSpace(result); attempt++)
        {
            try
            {
                var youtubeClient = new YoutubeClient();
                var manifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoId, ct);
                var streamInfo = SelectBestStream(manifest);

                if (streamInfo is not null)
                {
                    var fileName = string.Concat(
                        BuildSafeFileName(track.Title, videoId),
                        ".",
                        GetStreamExtension(streamInfo)
                    );
                    var filePath = Path.Combine(outputDirectory, fileName);

                    await youtubeClient.Videos.Streams.DownloadAsync(
                        streamInfo,
                        filePath,
                        null,
                        ct
                    );

                    if (File.Exists(filePath))
                    {
                        result = filePath;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "[YouTubeResolver] Попытка {Attempt} скачать через YoutubeReExplode завершилась ошибкой для videoId={VideoId}",
                    attempt,
                    videoId
                );
            }
        }

        return result;
    }

    private async Task<string?> TryDownloadWithYtDlpAsync(
        BaseTrackInfo track,
        string videoId,
        string outputDirectory,
        CancellationToken ct
    )
    {
        string? result = null;
        var videoUrl = BuildVideoUrl(track, videoId);

        if (!string.IsNullOrWhiteSpace(videoUrl))
        {
            var safeBaseName = BuildSafeFileName(track.Title, videoId);
            var outputTemplate = Path.Combine(
                outputDirectory,
                string.Concat(safeBaseName, ".%(ext)s")
            );
            var existingFiles = Directory
                .GetFiles(outputDirectory, string.Concat(safeBaseName, ".*"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var process = new Process();

                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                process.StartInfo.ArgumentList.Add("--no-playlist");
                process.StartInfo.ArgumentList.Add("-f");
                process.StartInfo.ArgumentList.Add("bestaudio/best");
                process.StartInfo.ArgumentList.Add("-o");
                process.StartInfo.ArgumentList.Add(outputTemplate);
                process.StartInfo.ArgumentList.Add(videoUrl);

                if (process.Start())
                {
                    var standardErrorTask = process.StandardError.ReadToEndAsync(ct);
                    var standardOutputTask = process.StandardOutput.ReadToEndAsync(ct);

                    await process.WaitForExitAsync(ct);

                    var standardError = await standardErrorTask;
                    await standardOutputTask;

                    if (process.ExitCode == 0)
                    {
                        result = FindDownloadedFile(outputDirectory, safeBaseName, existingFiles);
                    }
                    else
                    {
                        logger.LogWarning(
                            "[YouTubeResolver] yt-dlp завершился с кодом {ExitCode} для videoId={VideoId}. stderr: {StandardError}",
                            process.ExitCode,
                            videoId,
                            standardError
                        );
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                logger.LogWarning(
                    "[YouTubeResolver] yt-dlp не найден в PATH. videoId={VideoId}",
                    videoId
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "[YouTubeResolver] Ошибка fallback-загрузки через yt-dlp для videoId={VideoId}",
                    videoId
                );
            }
        }

        return result;
    }

    private static IStreamInfo? SelectBestStream(StreamManifest manifest)
    {
        IStreamInfo? result = manifest
            .GetAudioOnlyStreams()
            .OrderByDescending(stream => stream.Bitrate)
            .FirstOrDefault();

        result ??= manifest
            .GetMuxedStreams()
            .OrderByDescending(stream => stream.Bitrate)
            .FirstOrDefault();

        return result;
    }

    private static string BuildVideoUrl(BaseTrackInfo track, string videoId)
    {
        var result = !string.IsNullOrWhiteSpace(track?.Url?.ToString())
            ? track.Url.ToString()
            : string.Concat("https://www.youtube.com/watch?v=", videoId);

        return result;
    }

    private static string? FindDownloadedFile(
        string outputDirectory,
        string safeBaseName,
        HashSet<string> existingFiles
    )
    {
        string? result = null;

        var candidates = Directory
            .GetFiles(outputDirectory, string.Concat(safeBaseName, ".*"))
            .Where(path =>
                !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)
            )
            .Where(path => !existingFiles.Contains(path))
            .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
            .ToArray();

        if (candidates.Length == 0)
        {
            candidates = Directory
                .GetFiles(outputDirectory, string.Concat(safeBaseName, ".*"))
                .Where(path =>
                    !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)
                )
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                .ToArray();
        }

        if (candidates.Length > 0)
        {
            result = candidates[0];
        }

        return result;
    }

    private static BaseTrackInfo CreateTrackInfo(
        string url,
        string videoId,
        string trackName,
        string? author,
        TimeSpan duration,
        string? thumbnailUrl
    )
    {
        string[] authors = !string.IsNullOrWhiteSpace(author) ? [author] : [];
        var result = new BaseTrackInfo
        {
            Id = Guid.NewGuid(),
            Url = new Uri(url),
            VideoId = videoId,
            TrackName = trackName,
            Authors = authors,
            Duration = duration,
            ArtworkUrl = !string.IsNullOrWhiteSpace(thumbnailUrl) ? new Uri(thumbnailUrl) : null,
        };

        return result;
    }

    private static string GetStreamExtension(IStreamInfo streamInfo)
    {
        var result = streamInfo.Container.Name;

        if (streamInfo is IAudioStreamInfo && streamInfo.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
        {
            result = "m4a";
        }

        return result;
    }

    private static string BuildSafeFileName(string title, string fallback)
    {
        var result = fallback;

        if (!string.IsNullOrWhiteSpace(title))
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeChars = title.Select(character =>
                invalidChars.Contains(character) ? '_' : character
            );
            var normalizedTitle = new string(safeChars.ToArray()).Trim();
            if (normalizedTitle.Length > 80)
            {
                normalizedTitle = normalizedTitle[..80].Trim();
            }

            if (!string.IsNullOrWhiteSpace(normalizedTitle))
            {
                result = normalizedTitle;
            }
        }

        return result;
    }

    private static string? TryExtractVideoId(string url)
    {
        string? result = null;

        if (!string.IsNullOrWhiteSpace(url))
        {
            var match = Regex.Match(
                url,
                @"(?:youtu\.be/|youtube\.com/(?:watch\?v=|shorts/|embed/))([^?&/]+)",
                RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                result = match.Groups[1].Value;
            }
        }

        return result;
    }
}
