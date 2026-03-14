using System.Text.RegularExpressions;
using MARS.Server.Services.SoundRequest.Entities;
using YoutubeReExplode;
using YoutubeReExplode.Videos.Streams;

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
        BaseTrackInfo track,
        string outputDirectory,
        CancellationToken ct
    )
    {
        string? result = null;

        if (track is not null && !string.IsNullOrWhiteSpace(outputDirectory))
        {
            var videoId = GetVideoId(track);
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                try
                {
                    Directory.CreateDirectory(outputDirectory);

                    var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId, ct);
                    IStreamInfo? streamInfo = manifest
                        .GetAudioOnlyStreams()
                        .OrderByDescending(stream => stream.Bitrate)
                        .FirstOrDefault();

                    streamInfo ??= manifest
                        .GetMuxedStreams()
                        .OrderByDescending(stream => stream.Bitrate)
                        .FirstOrDefault();

                    if (streamInfo is not null)
                    {
                        var fileName = string.Concat(
                            BuildSafeFileName(track.Title, videoId),
                            ".",
                            GetStreamExtension(streamInfo)
                        );
                        var filePath = Path.Combine(outputDirectory, fileName);

                        await _youtubeClient.Videos.Streams.DownloadAsync(
                            streamInfo,
                            filePath,
                            null,
                            ct
                        );

                        result = filePath;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                }
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

        if (streamInfo is IAudioStreamInfo && streamInfo.Container == Container.Mp4)
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