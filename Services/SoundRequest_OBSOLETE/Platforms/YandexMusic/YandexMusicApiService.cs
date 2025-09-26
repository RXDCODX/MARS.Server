using System.Text.RegularExpressions;
using MARS.Server.Services.MemoryStorageService;
using MARS.Server.Services.SoundRequest.Entitys;
using YandexMusicResolver;

namespace MARS.Server.Services.SoundRequest.Platforms.YandexMusic;

public class YandexMusicApiService(IYandexMusicMainResolver resolver, IHttpClientFactory factory)
{
    public async Task<BaseTrackInfo> GetYandexMusicBaseTrackInfoAsync(
        string url,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Пустой или некорректный URL Yandex Music.");
        }

        // Извлекаем trackId из ссылки
        var match = Regex.Match(url, @"track/(\d+)");
        if (!match.Success)
        {
            throw new ArgumentException(
                "Не удалось извлечь идентификатор трека из ссылки Yandex Music."
            );
        }

        var trackId = match.Groups[1].Value;

        // Получаем информацию о треке
        var track =
            await resolver.TrackLoader.LoadTrack(trackId)
            ?? throw new Exception("Трек не найден на Yandex Music.");
        using var httpClient = factory.CreateClient("YandexMusicApiTextQuery");

        var yaDwTrck = await resolver.DirectUrlLoader.GetDirectUrl(track.Id);

        var filePath = await MemoryStorage.AddFileAsync(
            track.Author + " + " + track.Title,
            await httpClient.GetByteArrayAsync(yaDwTrck, ct)
        );

        return new BaseTrackInfo
        {
            Id = Guid.NewGuid(),
            TrackName = track.Title,
            Authors = [.. track.Authors.Select(a => a.Name)],
            Duration = track.Length,
            Url = url,
            Genre = null,
            YandexSpecificInfo = new YandexTrackAdditionalInfo()
            {
                ArtworkUrl = track.ArtworkUrl,
                Mp3TrackUrl = filePath,
            },
        };
    }
}
