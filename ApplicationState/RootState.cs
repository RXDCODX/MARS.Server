namespace MARS.Server.ApplicationState;

public partial class RootState
{
    [Key]
    public required string Name { get; set; }

    public string Value { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string TypeDescription { get; set; } = string.Empty;
}

public static class RootStateKeys
{
    public const string RandomMemeOnlineIsStop = "RandomMemeOnlineIsStop";
    public const string PuntoSwitcherFilterEnabled = "PuntoSwitcherFilterEnabled";
    public const string WaifuRollCooldownMinutes = "WaifuRollCooldownMinutes";
    public const string WTelegramMtProxyUrl = "WTelegramMtProxyUrl";
    public const string SoundRequestSpotifyClientId = "SoundRequestSpotifyClientId";
    public const string SoundRequestSpotifyClientSecret = "SoundRequestSpotifyClientSecret";
    public const string SoundRequestSpotifyRefreshToken = "SoundRequestSpotifyRefreshToken";
    public const string SoundRequestSpotifyAccessToken = "SoundRequestSpotifyAccessToken";
    public const string SoundRequestSpotifyAccessTokenExpiresAtUtc = "SoundRequestSpotifyAccessTokenExpiresAtUtc";
    public const string SoundRequestSpotifyDisplayName = "SoundRequestSpotifyDisplayName";
    public const string SoundRequestSpotifyUserId = "SoundRequestSpotifyUserId";
    public const string SoundRequestSpotifyAvatarUrl = "SoundRequestSpotifyAvatarUrl";
    public const string SoundRequestSpotifyProduct = "SoundRequestSpotifyProduct";
    public const string SoundRequestSpotifyDeviceId = "SoundRequestSpotifyDeviceId";
    public const string SoundRequestSpotifyOAuthState = "SoundRequestSpotifyOAuthState";
}
