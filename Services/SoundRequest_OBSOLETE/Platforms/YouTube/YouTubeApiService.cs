using System.Text.Json;
using MARS.Server.Services.SoundRequest_OBSOLETE.Entitys;

namespace MARS.Server.Services.SoundRequest_OBSOLETE.Platforms.YouTube;

public class YouTubeApiService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("SoundRequest");

    public async Task<BaseTrackInfo> GetYoutubeBaseTrackInfoAsync(
        string url,
        string apiKey,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Пустой или некорректный URL YouTube.");
        }

        var videoId = ExtractYoutubeVideoId(url);
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new ArgumentException(
                "Не удалось извлечь идентификатор видео из ссылки YouTube."
            );
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("YouTube API key is not configured.");
        }

        var apiUrl =
            $"https://www.googleapis.com/youtube/v3/videos?part=snippet,contentDetails&id={videoId}&key={apiKey}";
        var response = await _httpClient.GetAsync(apiUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"YouTube API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
        {
            throw new Exception("Видео не найдено на YouTube.");
        }

        try
        {
            var item = items[0];
            var snippet = item.GetProperty("snippet");
            var contentDetails = item.GetProperty("contentDetails");
            var title = snippet.GetProperty("title").GetString() ?? "Unknown Title";
            var channelTitle = snippet.GetProperty("channelTitle").GetString() ?? "Unknown Author";
            var durationIso = contentDetails.GetProperty("duration").GetString() ?? "PT0S";
            var duration = System.Xml.XmlConvert.ToTimeSpan(durationIso);
            return new BaseTrackInfo
            {
                Id = Guid.NewGuid(),
                TrackName = title,
                Authors = [channelTitle],
                Duration = duration,
                Url = url,
                Genre = null,
                FeatAuthors = null,
                LastTimePlays = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            throw new Exception("Ошибка при обработке данных о видео с YouTube.", ex);
        }
    }

    private static string? ExtractYoutubeVideoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var uri = new Uri(url);
        if (uri.Host.Contains("youtu.be"))
        {
            return uri.AbsolutePath.Trim('/');
        }
        else if (uri.Host.Contains("youtube.com"))
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return query["v"];
        }
        return null;
    }
}
