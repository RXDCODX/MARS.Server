namespace MARS.Server.Services.SoundRequest.Spotify;

public class SpotifyPlaybackService(SpotifyApiClient spotifyApiClient)
{
    public bool IsConfigured()
    {
        return spotifyApiClient.IsConfigured();
    }

    public bool IsSpotifyTrack(BaseTrackInfo? track)
    {
        var result =
            track != null
            && !string.IsNullOrWhiteSpace(track.VideoId)
            && track.VideoId.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase);

        return result;
    }

    public string? GetSpotifyTrackId(BaseTrackInfo? track)
    {
        string? result = null;

        if (track != null)
        {
            if (
                !string.IsNullOrWhiteSpace(track.VideoId)
                && track.VideoId.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase)
            )
            {
                result = track.VideoId.Split(':').LastOrDefault();
            }
            else if (track.Url != null)
            {
                result = spotifyApiClient.ExtractTrackId(track.Url.ToString());
            }
        }

        return result;
    }

    public async Task<bool> PlayTrackAsync(BaseTrackInfo? track, CancellationToken ct)
    {
        var result = false;

        var trackId = GetSpotifyTrackId(track);
        if (!string.IsNullOrWhiteSpace(trackId))
        {
            result = await spotifyApiClient.PlayTrackAsync(trackId, ct);
        }

        return result;
    }

    public async Task<bool> PauseAsync(CancellationToken ct)
    {
        return await spotifyApiClient.PauseAsync(ct);
    }

    public async Task<bool> ResumeAsync(CancellationToken ct)
    {
        return await spotifyApiClient.ResumeAsync(ct);
    }

    public async Task<bool> StopAsync(CancellationToken ct)
    {
        return await spotifyApiClient.PauseAsync(ct);
    }

    public async Task<bool> SkipAsync(CancellationToken ct)
    {
        return await spotifyApiClient.SkipToNextAsync(ct);
    }

    public async Task<bool> SetVolumeAsync(int volume, CancellationToken ct)
    {
        return await spotifyApiClient.SetVolumeAsync(volume, ct);
    }

    public async Task<SpotifyPlaybackSnapshot?> GetCurrentPlaybackAsync(CancellationToken ct)
    {
        return await spotifyApiClient.GetCurrentPlaybackAsync(ct);
    }
}
