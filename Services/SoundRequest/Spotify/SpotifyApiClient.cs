using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MARS.Server.Configuration;
using MARS.Server.Services.SoundRequest.Entities;
using Microsoft.Extensions.Options;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace MARS.Server.Services.SoundRequest.Spotify;

public class SpotifyApiClient(
    HttpClient httpClient,
    SpotifyAuthService spotifyAuthService,
    IOptions<SpotifySoundRequestConfiguration> options,
    ILogger<SpotifyApiClient> logger
)
{
    private const string ApiBaseUrl = "https://api.spotify.com/v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly SpotifySoundRequestConfiguration _settings = options.Value;
    private string? _cachedResolvedDeviceId;
    private DateTime _cachedResolvedDeviceIdAtUtc = DateTime.UnixEpoch;

    public bool IsConfigured()
    {
        var result = _settings.Enabled;

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
                var dto = JsonSerializer.Deserialize<SpotifySearchResponseDto>(
                    payload,
                    JsonOptions
                );
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
            var url = await BuildPlayerUrlAsync("play", ct);
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

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            await BuildPlayerUrlAsync("play", ct),
            ct
        );
        result = response?.IsSuccessStatusCode == true;

        return result;
    }

    public async Task<bool> PauseAsync(CancellationToken ct)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            await BuildPlayerUrlAsync("pause", ct),
            ct
        );
        return response?.IsSuccessStatusCode == true;
    }

    public async Task<bool> SkipToNextAsync(CancellationToken ct)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            await BuildPlayerUrlAsync("next", ct),
            ct
        );
        return response?.IsSuccessStatusCode == true;
    }

    public async Task<bool> SetVolumeAsync(int volume, CancellationToken ct)
    {
        var clampedVolume = Math.Clamp(volume, 0, 100);
        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            await BuildPlayerUrlAsync($"volume?volume_percent={clampedVolume}", ct),
            ct
        );

        return response?.IsSuccessStatusCode == true;
    }

    public async Task<SpotifyPlaybackSnapshot?> GetCurrentPlaybackAsync(CancellationToken ct)
    {
        SpotifyPlaybackSnapshot? result = null;

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            await BuildPlayerUrlAsync(string.Empty, ct),
            ct
        );

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
                    if (
                        segments.Length >= 2
                        && segments[0].Equals("track", StringComparison.OrdinalIgnoreCase)
                    )
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

        var accessTokenResult = await spotifyAuthService.GetValidAccessTokenAsync(ct);

        if (accessTokenResult.Success)
        {
            var resolvedDeviceId = await ResolveDeviceIdAsync(accessTokenResult.AccessToken, ct);

            if (string.IsNullOrWhiteSpace(resolvedDeviceId))
            {
                resolvedDeviceId = accessTokenResult.DeviceId;
            }

            if (string.IsNullOrWhiteSpace(resolvedDeviceId))
            {
                resolvedDeviceId = _settings.DeviceId;
            }

            var requestBody = JsonSerializer.Serialize(
                new { device_ids = new[] { resolvedDeviceId }, play = false }
            );

            var response = await SendAuthorizedAsync(
                HttpMethod.Put,
                await BuildPlayerUrlAsync(string.Empty, ct),
                ct,
                new StringContent(requestBody, Encoding.UTF8, "application/json")
            );

            result = response?.IsSuccessStatusCode == true;
        }

        return result;
    }

    private async Task<string> BuildPlayerUrlAsync(string endpoint, CancellationToken ct)
    {
        var result = $"{ApiBaseUrl}/me/player";

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var trimmed = endpoint.TrimStart('/');
            result = $"{result}/{trimmed}";
        }

        var accessTokenResult = await spotifyAuthService.GetValidAccessTokenAsync(ct);
        var resolvedDeviceId = string.Empty;

        if (accessTokenResult.Success)
        {
            resolvedDeviceId = await ResolveDeviceIdAsync(accessTokenResult.AccessToken, ct);

            if (string.IsNullOrWhiteSpace(resolvedDeviceId))
            {
                resolvedDeviceId = accessTokenResult.DeviceId;
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedDeviceId))
        {
            resolvedDeviceId = _settings.DeviceId;
        }

        if (!string.IsNullOrWhiteSpace(resolvedDeviceId))
        {
            var separator = result.Contains('?') ? "&" : "?";
            result = $"{result}{separator}device_id={Uri.EscapeDataString(resolvedDeviceId)}";
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

        var accessToken = await spotifyAuthService.GetValidAccessTokenAsync(ct);

        if (accessToken.Success && !string.IsNullOrWhiteSpace(accessToken.AccessToken))
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                accessToken.AccessToken
            );
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
                logger.LogError(
                    ex,
                    "Spotify API request exception: {Method} {Url}",
                    method.Method,
                    url
                );
            }
        }
        else
        {
            logger.LogWarning("Spotify token is not available: {Message}", accessToken.Message);
        }

        return result;
    }

    private async Task<string> ResolveDeviceIdAsync(string accessToken, CancellationToken ct)
    {
        var result = string.Empty;

        if (
            !string.IsNullOrWhiteSpace(_cachedResolvedDeviceId)
            && _cachedResolvedDeviceIdAtUtc > DateTime.Now.AddMinutes(-2)
        )
        {
            result = _cachedResolvedDeviceId;
        }
        else if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{ApiBaseUrl}/me/player/devices"
                );
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken
                );

                using var response = await httpClient.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadAsStringAsync(ct);
                    var dto = JsonSerializer.Deserialize<SpotifyDevicesResponseDto>(
                        payload,
                        JsonOptions
                    );

                    var activeDevice = dto?.Devices?.FirstOrDefault(d => d.IsActive == true);
                    var fallbackDevice = dto?.Devices?.FirstOrDefault();
                    var resolved = activeDevice?.Id ?? fallbackDevice?.Id;

                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        _cachedResolvedDeviceId = resolved;
                        _cachedResolvedDeviceIdAtUtc = DateTime.Now;
                        await spotifyAuthService.SaveDeviceIdAsync(resolved, ct);
                        result = resolved;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Не удалось получить список Spotify устройств");
            }
        }

        return result;
    }

    private static BaseTrackInfo? MapTrack(SpotifyTrackDto? track)
    {
        BaseTrackInfo? result = null;

        if (track?.Id is not null)
        {
            var artists = track
                .Artists?.Where(a => !string.IsNullOrWhiteSpace(a.Name))
                .Select(a => a.Name!)
                .ToArray();
            var artwork = track
                .Album?.Images?.OrderByDescending(i => i.Width)
                .FirstOrDefault()
                ?.Url;

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

    private class SpotifyDevicesResponseDto
    {
        public List<SpotifyDeviceDto>? Devices { get; set; }
    }

    private class SpotifyDeviceDto
    {
        public string? Id { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }
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
