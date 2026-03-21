using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MARS.Server.Configuration;
using MARS.Server.Services.SoundRequest.Entities;
using Microsoft.Extensions.Options;

namespace MARS.Server.Services.SoundRequest.Spotify;

public class SpotifyApiClient(
    HttpClient httpClient,
    IOptions<SpotifySoundRequestConfiguration> options,
    ILogger<SpotifyApiClient> logger
)
{
    private const string AuthUrl = "https://accounts.spotify.com/api/token";
    private const string ApiBaseUrl = "https://api.spotify.com/v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly SpotifySoundRequestConfiguration _settings = options.Value;

    private string? _cachedAccessToken;
    private DateTime _cachedAccessTokenExpiresAtUtc = DateTime.UnixEpoch;

    public bool IsConfigured()
    {
        var result =
            _settings.Enabled
            && !string.IsNullOrWhiteSpace(_settings.ClientId)
            && !string.IsNullOrWhiteSpace(_settings.ClientSecret)
            && !string.IsNullOrWhiteSpace(_settings.RefreshToken);

        return result;
    }

    public async Task<BaseTrackInfo?> SearchTrackAsync(string query, CancellationToken ct)
    {
        BaseTrackInfo? result = null;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var encodedQuery = Uri.EscapeDataString(query.Trim());
            var market = string.IsNullOrWhiteSpace(_settings.Market) ? "RU" : _settings.Market;
            var url = $"{ApiBaseUrl}/search?type=track&limit=1&market={market}&q={encodedQuery}";
            var response = await SendAuthorizedAsync(HttpMethod.Get, url, ct);

            if (response?.IsSuccessStatusCode == true)
            {
                var payload = await response.Content.ReadAsStringAsync(ct);
                var dto = JsonSerializer.Deserialize<SpotifySearchResponseDto>(payload, JsonOptions);
                var track = dto?.Tracks?.Items?.FirstOrDefault();
                result = MapTrack(track);
            }
        }

        return result;
    }

    public async Task<BaseTrackInfo?> ResolveTrackAsync(string queryOrUrl, CancellationToken ct)
    {
        BaseTrackInfo? result = null;

        if (!string.IsNullOrWhiteSpace(queryOrUrl))
        {
            var trackId = ExtractTrackId(queryOrUrl);

            if (!string.IsNullOrWhiteSpace(trackId))
            {
                var market = string.IsNullOrWhiteSpace(_settings.Market) ? "RU" : _settings.Market;
                var url = $"{ApiBaseUrl}/tracks/{trackId}?market={market}";
                var response = await SendAuthorizedAsync(HttpMethod.Get, url, ct);

                if (response?.IsSuccessStatusCode == true)
                {
                    var payload = await response.Content.ReadAsStringAsync(ct);
                    var track = JsonSerializer.Deserialize<SpotifyTrackDto>(payload, JsonOptions);
                    result = MapTrack(track);
                }
            }
        }

        return result;
    }

    public async Task<bool> PlayTrackAsync(string spotifyTrackId, CancellationToken ct)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(spotifyTrackId))
        {
            if (_settings.ForceDeviceTransfer)
            {
                await TransferPlaybackAsync(ct);
            }

            var uri = $"spotify:track:{spotifyTrackId}";
            var requestBody = JsonSerializer.Serialize(new { uris = new[] { uri } });
            var url = BuildPlayerUrl("play");
            var response = await SendAuthorizedAsync(
                HttpMethod.Put,
                url,
                ct,
                new StringContent(requestBody, Encoding.UTF8, "application/json")
            );

            result = response?.IsSuccessStatusCode == true;
        }

        return result;
    }

    public async Task<bool> ResumeAsync(CancellationToken ct)
    {
        var result = false;

        if (_settings.ForceDeviceTransfer)
        {
            await TransferPlaybackAsync(ct);
        }

        var response = await SendAuthorizedAsync(HttpMethod.Put, BuildPlayerUrl("play"), ct);
        result = response?.IsSuccessStatusCode == true;

        return result;
    }

    public async Task<bool> PauseAsync(CancellationToken ct)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Put, BuildPlayerUrl("pause"), ct);
        return response?.IsSuccessStatusCode == true;
    }

    public async Task<bool> SkipToNextAsync(CancellationToken ct)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Post, BuildPlayerUrl("next"), ct);
        return response?.IsSuccessStatusCode == true;
    }

    public async Task<bool> SetVolumeAsync(int volume, CancellationToken ct)
    {
        var clampedVolume = Math.Clamp(volume, 0, 100);
        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            BuildPlayerUrl($"volume?volume_percent={clampedVolume}"),
            ct
        );

        return response?.IsSuccessStatusCode == true;
    }

    public async Task<SpotifyPlaybackSnapshot?> GetCurrentPlaybackAsync(CancellationToken ct)
    {
        SpotifyPlaybackSnapshot? result = null;

        var response = await SendAuthorizedAsync(HttpMethod.Get, BuildPlayerUrl(string.Empty), ct);

        if (response?.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            result = new SpotifyPlaybackSnapshot();
        }
        else if (response?.IsSuccessStatusCode == true)
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<SpotifyPlaybackStateDto>(payload, JsonOptions);

            result = new SpotifyPlaybackSnapshot
            {
                IsPlaying = dto?.IsPlaying == true,
                TrackId = dto?.Item?.Id,
                ProgressMs = dto?.ProgressMs ?? 0,
                DurationMs = dto?.Item?.DurationMs ?? 0,
            };
        }

        return result;
    }

    public string? ExtractTrackId(string queryOrUrl)
    {
        string? result = null;

        if (!string.IsNullOrWhiteSpace(queryOrUrl))
        {
            var input = queryOrUrl.Trim();

            if (input.StartsWith("spotify:track:", StringComparison.OrdinalIgnoreCase))
            {
                result = input.Split(':').LastOrDefault();
            }
            else if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                if (uri.Host.Contains("spotify.com", StringComparison.OrdinalIgnoreCase))
                {
                    var segments = uri.AbsolutePath.Trim('/').Split('/');
                    if (segments.Length >= 2 && segments[0].Equals("track", StringComparison.OrdinalIgnoreCase))
                    {
                        result = segments[1];
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(result))
            {
                var clean = result.Split('?')[0].Split('&')[0].Trim();
                result = clean;
            }
        }

        return result;
    }

    private async Task<bool> TransferPlaybackAsync(CancellationToken ct)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(_settings.DeviceId))
        {
            var requestBody = JsonSerializer.Serialize(
                new
                {
                    device_ids = new[] { _settings.DeviceId },
                    play = false,
                }
            );

            var response = await SendAuthorizedAsync(
                HttpMethod.Put,
                BuildPlayerUrl(string.Empty),
                ct,
                new StringContent(requestBody, Encoding.UTF8, "application/json")
            );

            result = response?.IsSuccessStatusCode == true;
        }

        return result;
    }

    private string BuildPlayerUrl(string endpoint)
    {
        var result = $"{ApiBaseUrl}/me/player";

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var trimmed = endpoint.TrimStart('/');
            result = $"{result}/{trimmed}";
        }

        if (!string.IsNullOrWhiteSpace(_settings.DeviceId))
        {
            var separator = result.Contains('?') ? "&" : "?";
            result = $"{result}{separator}device_id={Uri.EscapeDataString(_settings.DeviceId)}";
        }

        return result;
    }

    private async Task<HttpResponseMessage?> SendAuthorizedAsync(
        HttpMethod method,
        string url,
        CancellationToken ct,
        HttpContent? content = null
    )
    {
        HttpResponseMessage? result = null;

        var token = await GetAccessTokenAsync(ct);
        if (!string.IsNullOrWhiteSpace(token))
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;

            try
            {
                result = await httpClient.SendAsync(request, ct);
                if (!result.IsSuccessStatusCode)
                {
                    var errorBody = await result.Content.ReadAsStringAsync(ct);
                    logger.LogWarning(
                        "Spotify API request failed: {Method} {Url} Status={Status} Body={Body}",
                        method.Method,
                        url,
                        (int)result.StatusCode,
                        errorBody
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Spotify API request exception: {Method} {Url}", method.Method, url);
            }
        }

        return result;
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        string? result = null;

        if (_cachedAccessTokenExpiresAtUtc > DateTime.UtcNow.AddSeconds(30))
        {
            result = _cachedAccessToken;
        }
        else if (IsConfigured())
        {
            try
            {
                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _settings.RefreshToken,
                    ["client_id"] = _settings.ClientId,
                    ["client_secret"] = _settings.ClientSecret,
                };

                using var content = new FormUrlEncodedContent(form);
                using var response = await httpClient.PostAsync(AuthUrl, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadAsStringAsync(ct);
                    var dto = JsonSerializer.Deserialize<SpotifyTokenResponseDto>(payload, JsonOptions);

                    if (!string.IsNullOrWhiteSpace(dto?.AccessToken))
                    {
                        _cachedAccessToken = dto.AccessToken;
                        _cachedAccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(
                            Math.Max(60, dto.ExpiresIn - 30)
                        );
                        result = _cachedAccessToken;
                    }
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    logger.LogWarning(
                        "Spotify token request failed: Status={Status} Body={Body}",
                        (int)response.StatusCode,
                        body
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Spotify token request exception");
            }
        }

        return result;
    }

    private static BaseTrackInfo? MapTrack(SpotifyTrackDto? track)
    {
        BaseTrackInfo? result = null;

        if (track?.Id is not null)
        {
            var artists = track.Artists?.Where(a => !string.IsNullOrWhiteSpace(a.Name)).Select(a => a.Name!).ToArray();
            var artwork = track.Album?.Images?.OrderByDescending(i => i.Width).FirstOrDefault()?.Url;

            result = new BaseTrackInfo
            {
                Id = Guid.NewGuid(),
                VideoId = $"spotify:{track.Id}",
                Url = new Uri($"https://open.spotify.com/track/{track.Id}"),
                TrackName = track.Name ?? "Unknown track",
                Authors = artists,
                Duration = TimeSpan.FromMilliseconds(Math.Max(0, track.DurationMs)),
                ArtworkUrl = !string.IsNullOrWhiteSpace(artwork) ? new Uri(artwork) : null,
            };
        }

        return result;
    }

    private class SpotifyTokenResponseDto
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private class SpotifySearchResponseDto
    {
        public SpotifyTracksContainerDto? Tracks { get; set; }
    }

    private class SpotifyTracksContainerDto
    {
        public List<SpotifyTrackDto>? Items { get; set; }
    }

    private class SpotifyTrackDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("duration_ms")]
        public int DurationMs { get; set; }

        public SpotifyAlbumDto? Album { get; set; }

        public List<SpotifyArtistDto>? Artists { get; set; }
    }

    private class SpotifyAlbumDto
    {
        public List<SpotifyImageDto>? Images { get; set; }
    }

    private class SpotifyImageDto
    {
        public string? Url { get; set; }

        public int Width { get; set; }
    }

    private class SpotifyArtistDto
    {
        public string? Name { get; set; }
    }

    private class SpotifyPlaybackStateDto
    {
        [JsonPropertyName("is_playing")]
        public bool IsPlaying { get; set; }

        [JsonPropertyName("progress_ms")]
        public int ProgressMs { get; set; }

        public SpotifyTrackDto? Item { get; set; }
    }

}
