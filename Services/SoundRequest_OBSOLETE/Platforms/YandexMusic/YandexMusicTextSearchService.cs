using MARS.Server.Services.MemoryStorageService;
using MARS.Server.Services.SoundRequest_OBSOLETE.Entitys;
using YandexMusicResolver;

namespace MARS.Server.Services.SoundRequest_OBSOLETE.Platforms.YandexMusic;

public class YandexMusicTextSearchService(
    IYandexMusicMainResolver resolver,
    IHttpClientFactory factory
)
{
    public async Task<BaseTrackInfo> SearchTracksAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Пустой поисковый запрос для Яндекс Музыки.");
        }

        if (!resolver.AllowSearch)
        {
            throw new Exception("Yandex Music Search Not Allowed");
        }

        // ВНИМАНИЕ: Не удалось определить property для поиска треков по тексту в IYandexMusicMainResolver.
        // Обычно это что-то вроде resolver.SearchLoader.SearchTracksAsync(query, ct)
        // или resolver.Search.SearchTracksAsync(query, ct)
        // Пожалуйста, проверьте документацию или исходники YandexMusicResolver.
        var searchResult = await resolver.SearchResultLoader.LoadSearchResult(
            YandexSearchType.Track,
            query,
            1
        );

        if (searchResult?.Tracks == null || searchResult.Tracks.Count == 0)
        {
            throw new NullReferenceException("Треки не найдены");
        }

        var yandexTrack = searchResult.Tracks.First();

        using var httpClient = factory.CreateClient("YandexMusicApiTextQuery");

        var yaDwTrck = await resolver.DirectUrlLoader.GetDirectUrl(yandexTrack.Id);

        var filePath = await MemoryStorage.AddFileAsync(
            yandexTrack.Author + " + " + yandexTrack.Title,
            await httpClient.GetByteArrayAsync(yaDwTrck, ct)
        );

        return new BaseTrackInfo()
        {
            Url = yandexTrack.Uri!,
            TrackName = yandexTrack.Title,
            Authors = [.. yandexTrack.Authors.Select(e => e.Name)],
            Domain = SoundRequestDomainSource.YandexMusic,
            YandexSpecificInfo = new YandexTrackAdditionalInfo()
            {
                ArtworkUrl = yandexTrack.ArtworkUrl,
                Mp3TrackUrl = filePath,
            },
            Duration = yandexTrack.Length,
        };
    }
}
