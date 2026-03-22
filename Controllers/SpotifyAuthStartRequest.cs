namespace MARS.Server.Controllers;

public class SpotifyAuthStartRequest
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string? RedirectUri { get; set; }
}
