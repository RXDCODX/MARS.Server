using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MARS.Server.Services.Obs;

public class HttpObsService : IObsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpObsService> _logger;

    public HttpObsService(
        HttpClient httpClient,
        IOptions<ObsConfiguration> config,
        ILogger<HttpObsService> logger
    )
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(config.Value.ServiceUrl);
        _logger = logger;
    }

    public bool IsConnected { get; private set; }

    public bool IsPaused { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("/api/obs/connect", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        await RefreshStatusAsync(cancellationToken);
    }

    public async void DisconnectAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/obs/disconnect", null);
            response.EnsureSuccessStatusCode();
            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disconnect from OBS via AudioController");
        }
    }

    public async Task<string> ScreenshotAsync(
        string? sourceName = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"/api/obs/screenshot?sourceName={sourceName ?? string.Empty}";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ScreenshotResponse>(
            cancellationToken: cancellationToken
        );

        return dto?.ScreenshotPath ?? string.Empty;
    }

    public async Task<ObsPauseResult> FreezeAsync(CancellationToken cancellationToken = default)
    {
        return await PostPauseCommandAsync("/api/obs/freeze", cancellationToken);
    }

    public async Task<ObsPauseResult> UnfreezeAsync(CancellationToken cancellationToken = default)
    {
        return await PostPauseCommandAsync("/api/obs/unfreeze", cancellationToken);
    }

    public async Task<ObsPauseResult> SwitchToPauseSceneAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await PostPauseCommandAsync("/api/obs/pause-scene", cancellationToken);
    }

    public async Task<ObsPauseResult> SwitchFromPauseSceneAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await PostPauseCommandAsync("/api/obs/unpause-scene", cancellationToken);
    }

    public async Task<ObsPauseResult> TogglePauseAsync(
        ObsPauseMode mode = ObsPauseMode.FreezeFrame,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"/api/obs/toggle?mode={(int)mode}";
        return await PostPauseCommandAsync(url, cancellationToken);
    }

    private async Task<ObsPauseResult> PostPauseCommandAsync(
        string url,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _httpClient.PostAsync(url, null, cancellationToken);

            var dto = await response.Content.ReadFromJsonAsync<PauseResultDto>(
                cancellationToken: cancellationToken
            );

            if (response.IsSuccessStatusCode && dto?.Success == true)
            {
                await RefreshStatusAsync(cancellationToken);
                return ObsPauseResult.Ok(dto.IsPaused, dto.ScreenshotPath);
            }

            return ObsPauseResult.Fail(dto?.Error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OBS command failed: {Url}", url);
            return ObsPauseResult.Fail(ex.Message);
        }
    }

    private async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                "/api/obs/status",
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            var status = await response.Content.ReadFromJsonAsync<StatusDto>(
                cancellationToken: cancellationToken
            );

            if (status != null)
            {
                IsConnected = status.IsConnected;
                IsPaused = status.IsPaused;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh OBS status");
        }
    }

    private sealed class StatusDto
    {
        [JsonPropertyName("isConnected")]
        public bool IsConnected { get; set; }

        [JsonPropertyName("isPaused")]
        public bool IsPaused { get; set; }
    }

    private sealed class PauseResultDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("isPaused")]
        public bool IsPaused { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("screenshotPath")]
        public string? ScreenshotPath { get; set; }
    }

    private sealed class ScreenshotResponse
    {
        [JsonPropertyName("screenshotPath")]
        public string? ScreenshotPath { get; set; }
    }
}
