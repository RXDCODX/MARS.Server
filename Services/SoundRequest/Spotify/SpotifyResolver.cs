using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.SoundRequest.Entities;

namespace MARS.Server.Services.SoundRequest.Spotify;

public class SpotifyResolver(SpotifyApiClient spotifyApiClient)
{
    public async Task<BaseTrackInfo?> ResolveQueryAsync(string query, CancellationToken ct)
    {
        BaseTrackInfo? result = null;

        if (!string.IsNullOrWhiteSpace(query))
        {
            result = await spotifyApiClient.SearchTrackAsync(query, ct);
        }

        return result;
    }

    public async Task<BaseTrackInfo?> ResolveTrackAsync(string queryOrUrl, CancellationToken ct)
    {
        BaseTrackInfo? result = null;

        if (!string.IsNullOrWhiteSpace(queryOrUrl))
        {
            result = await spotifyApiClient.ResolveTrackAsync(queryOrUrl, ct);
        }

        return result;
    }

    public string? ExtractTrackId(string queryOrUrl)
    {
        return spotifyApiClient.ExtractTrackId(queryOrUrl);
    }
}
