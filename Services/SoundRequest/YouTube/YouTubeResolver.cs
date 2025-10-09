using System.Text.RegularExpressions;
using System.Web;
using MARS.Server.Services.SoundRequest.Entities;
using VideoLibrary;

namespace MARS.Server.Services.SoundRequest.YouTube;

public class YouTubeResolver(IHttpClientFactory httpClientFactory, ILogger<YouTubeResolver> logger)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task<BaseTrackInfo?> ResolveVideoAsync(string url, CancellationToken ct)
    {
        BaseTrackInfo? result = null;

        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                using var youTube = Client.For(VideoLibrary.YouTube.Default);
                var video = await Task.Run(() => youTube.GetVideo(url), ct);

                result = new BaseTrackInfo
                {
                    Id = Guid.NewGuid(),
                    Url = url,
                    VideoId = ExtractVideoId(url),
                    TrackName = video.Title,
                    Authors = [video.FullName],
                    Duration = TimeSpan.Zero,
                    ArtworkUrl = null,
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
            // libvideo не поддерживает плейлисты напрямую; предполагаем внешний парсер (будет добавлено позже)
            result = [];
            await Task.CompletedTask;
        }

        return result;
    }

    private static string? ExtractVideoId(string url)
    {
        string? result = null;
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                if (uri.Host.Contains("youtu.be"))
                {
                    result = uri.AbsolutePath.Trim('/');
                }
                else if (uri.Host.Contains("youtube.com"))
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    result = query["v"];
                }
            }
        }

        return result;
    }
}
