using MARS.Server.Services.SoundRequest.Entities;
using YoutubeExplode;

namespace MARS.Server.Services.SoundRequest.YouTube;

public class YouTubeResolver(ILogger<YouTubeResolver> logger)
{
    private readonly YoutubeClient _youtubeClient = new();

    public async Task<BaseTrackInfo?> ResolveQueryAsync(string query, CancellationToken ct)
    {
        BaseTrackInfo? result = null;

        if (!string.IsNullOrWhiteSpace(query))
        {
            try
            {
                // Поиск видео по запросу
                var searchResults = _youtubeClient.Search.GetVideosAsync(query, ct);

                await foreach (var video in searchResults)
                {
                    var thumbnailUrl = video
                        .Thumbnails.OrderByDescending(t => t.Resolution.Area)
                        .FirstOrDefault()
                        ?.Url;

                    result = new BaseTrackInfo
                    {
                        Id = Guid.NewGuid(),
                        Url = new Uri(video.Url),
                        VideoId = video.Id,
                        TrackName = video.Title,
                        Authors = [video.Author.ChannelTitle],
                        Duration = video.Duration ?? TimeSpan.Zero,
                        ArtworkUrl = !string.IsNullOrWhiteSpace(thumbnailUrl)
                            ? new Uri(thumbnailUrl)
                            : null,
                    };

                    return result;
                }
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
                // YoutubeExplode может парсить URL напрямую
                var video = await _youtubeClient.Videos.GetAsync(url, ct);

                var thumbnailUrl = video
                    .Thumbnails.OrderByDescending(t => t.Resolution.Area)
                    .FirstOrDefault()
                    ?.Url;

                result = new BaseTrackInfo
                {
                    Id = Guid.NewGuid(),
                    Url = new Uri(url),
                    VideoId = video.Id,
                    TrackName = video.Title,
                    Authors = [video.Author.ChannelTitle],
                    Duration = video.Duration ?? TimeSpan.Zero,
                    ArtworkUrl = !string.IsNullOrWhiteSpace(thumbnailUrl)
                        ? new Uri(thumbnailUrl)
                        : null,
                };
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
                // YoutubeReExplode может парсить URL плейлиста напрямую
                var playlistVideos = _youtubeClient.Playlists.GetVideosAsync(playlistUrl);

                // Собираем видео из плейлиста (ограничиваем до 200 треков)
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
                        new BaseTrackInfo
                        {
                            Id = Guid.NewGuid(),
                            Url = new Uri(video.Url),
                            VideoId = video.Id,
                            TrackName = video.Title,
                            Authors = [video.Author.ChannelTitle],
                            Duration = video.Duration ?? TimeSpan.Zero,
                            ArtworkUrl = !string.IsNullOrWhiteSpace(thumbnailUrl)
                                ? new Uri(thumbnailUrl)
                                : null,
                        }
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
}
