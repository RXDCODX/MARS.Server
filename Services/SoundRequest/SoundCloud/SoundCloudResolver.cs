using MARS.Server.Services.SoundRequest.Entities;
using SoundCloudExplode;

namespace MARS.Server.Services.SoundRequest.SoundCloud;

public class SoundCloudResolver(ILogger<SoundCloudResolver> logger)
{
    private readonly SoundCloudClient _client = new();

    public async Task<BaseTrackInfo?> ResolveTrackAsync(
        string url,
        CancellationToken cancellationToken
    )
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
                            Authors = !string.IsNullOrWhiteSpace(authorName) ? [authorName] : null,
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

    public async Task<BaseTrackInfo[]?> ResolvePlaylistAsync(
        string playlistUrl,
        CancellationToken cancellationToken
    )
    {
        BaseTrackInfo[]? result = null;

        if (!string.IsNullOrWhiteSpace(playlistUrl))
        {
            try
            {
                var tracks = _client.Playlists.GetTracksAsync(playlistUrl, cancellationToken);
                var items = new List<BaseTrackInfo>();

                await foreach (var track in tracks)
                {
                    if (track?.PermalinkUrl == null)
                    {
                        continue;
                    }

                    var artworkUrl = track.ArtworkUrl?.ToString();
                    var authorName = track.User?.Username;
                    var sourceId = track.Id.ToString();
                    var permalinkUrl = track.PermalinkUrl;
                    var trackTitle = string.IsNullOrWhiteSpace(track.Title)
                        ? (string.IsNullOrWhiteSpace(track.PlaylistName)
                            ? "Unknown SoundCloud Track"
                            : track.PlaylistName)
                        : track.Title;
                    var duration = track.Duration.HasValue
                        ? TimeSpan.FromMilliseconds(track.Duration.Value)
                        : TimeSpan.Zero;

                    items.Add(
                        new BaseTrackInfo
                        {
                            Id = Guid.NewGuid(),
                            Url = permalinkUrl,
                            VideoId = $"soundcloud:{sourceId}",
                            TrackName = trackTitle,
                            Authors = !string.IsNullOrWhiteSpace(authorName) ? [authorName] : null,
                            Duration = duration,
                            ArtworkUrl = !string.IsNullOrWhiteSpace(artworkUrl)
                                ? new Uri(artworkUrl)
                                : null,
                        }
                    );
                }

                result = [.. items];
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
