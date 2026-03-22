namespace MARS.Server.Services.SoundRequest.Spotify;

public class SpotifyPlaybackSnapshot
{
    public bool IsPlaying { get; set; }

    public string? TrackId { get; set; }

    public int ProgressMs { get; set; }

    public int DurationMs { get; set; }
}
