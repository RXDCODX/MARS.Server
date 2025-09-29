using MARS.Server.Services.SoundRequest_OBSOLETE.Entitys;
using SoundCloudExplode;

namespace MARS.Server.Services.SoundRequest_OBSOLETE.Platforms.SoundCloud;

public class SoundCloudApiService
{
    private readonly SoundCloudClient _client = new();

    public async Task<BaseTrackInfo> GetSoundCloudBaseTrackInfoAsync(
        string url,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Пустой или некорректный URL SoundCloud.");
        }

        var track =
            await _client.Tracks.GetAsync(url, ct)
            ?? throw new Exception("Трек не найден на SoundCloud.");

        return new BaseTrackInfo
        {
            Id = Guid.NewGuid(),
            TrackName = track.Title ?? "Unknown Title",
            Authors = track.User is not null ? [track.User.Username ?? "Unknown Author"] : null,
            Duration = track.Duration.HasValue
                ? TimeSpan.FromMilliseconds(track.Duration.Value)
                : TimeSpan.Zero,
            Url = url,
            Genre = !string.IsNullOrWhiteSpace(track.Genre) ? [track.Genre] : null,
            FeatAuthors = null,
            LastTimePlays = DateTime.UtcNow,
        };
    }
}
