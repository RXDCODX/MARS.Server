using MARS.Server.Services.SoundRequest.Entitys;
using MARS.Server.Services.SoundRequest.Entitys.Exceptions;
using SoundCloudExplode;

namespace MARS.Server.Services.SoundRequest.Platforms.SoundCloud;

public class SoundCloudTextSearchService
{
    private readonly SoundCloudClient _client = new();

    public async Task<BaseTrackInfo> SearchTrackAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Пустой поисковый запрос для SoundCloud.");
        }

        // Получаем результаты поиска (только треки)
        await foreach (var track in _client.Search.GetTracksAsync(query, ct))
        {
            if (string.IsNullOrWhiteSpace(track.Url))
            {
                continue; // Пропускаем треки без ссылки
            }
            // Берём первый найденный трек с валидным Url
            return new BaseTrackInfo
            {
                Id = Guid.NewGuid(),
                TrackName = track.Title ?? "Unknown Title",
                Authors = track.User is not null ? [track.User.Username ?? "Unknown Author"] : null,
                Duration = track.Duration.HasValue
                    ? TimeSpan.FromMilliseconds(track.Duration.Value)
                    : TimeSpan.Zero,
                Url = track.Url!,
                Genre = !string.IsNullOrWhiteSpace(track.Genre) ? [track.Genre] : null,
                FeatAuthors = null,
                LastTimePlays = DateTime.UtcNow,
                Domain = SoundRequestDomainSource.SoundCloud,
            };
        }

        throw new TrackNotFoundException("Треки не найдены на SoundCloud по данному запросу.");
    }
}
