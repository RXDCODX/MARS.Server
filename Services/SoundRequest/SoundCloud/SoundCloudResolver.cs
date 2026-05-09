using MARS.Server.Services.SoundRequest.Entities;
using SoundCloudExplode;

namespace MARS.Server.Services.SoundRequest.SoundCloud;

public class SoundCloudResolver(ILogger<SoundCloudResolver> logger)
{
    private readonly SoundCloudClient _client = new();

    public async Task<BaseTrackInfo?> ResolveTrackAsync(string url, CancellationToken cancellationToken)
    {
        BaseTrackInfo? result = null;

        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                var track = await _client.Tracks.GetAsync(url, cancellationToken);
                if (track != null)
                {
                    var artworkUrl = track.ArtworkUrl?.ToString();
                    var authorName = track.User?.Username;
                    var sourceId = track.Id.ToString();
                    var permalinkUrl = track.PermalinkUrl;
                    var trackTitle = string.IsNullOrWhiteSpace(track.Title)
                        ? "Unknown SoundCloud Track"
                        : track.Title;
                    var duration = track.Duration.HasValue
                        ? TimeSpan.FromMilliseconds(track.Duration.Value)
                        : TimeSpan.Zero;

                    if (permalinkUrl != null)
                    {
                        result = new BaseTrackInfo
                        {
                            Id = Guid.NewGuid(),
                            Url = permalinkUrl,
                            VideoId = $"soundcloud:{sourceId}",
                            TrackName = trackTitle,
                            Authors = !string.IsNullOrWhiteSpace(authorName)
                                ? [authorName]
                                : null,
                            Duration = duration,
                            ArtworkUrl = !string.IsNullOrWhiteSpace(artworkUrl)
                                ? new Uri(artworkUrl)
                                : null,
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        return result;
    }
}
