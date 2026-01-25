using MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;

namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts;

public interface IFreeTtsHealthCheckService
{
    /// <summary>
    /// Checks if FreeTTS service is available and healthy
    /// </summary>
    Task<FreeTtsHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last health check result
    /// </summary>
    FreeTtsHealthResponse GetLastCheckResult();

    /// <summary>
    /// Gets the cached list of available voices
    /// </summary>
    Task<List<FreeTtsVoice>> GetCachedVoicesAsync(CancellationToken cancellationToken = default);
}

public class FreeTtsHealthCheckService : IFreeTtsHealthCheckService
{
    private readonly IFreeTtsHttpClient _httpClient;
    private readonly ILogger<FreeTtsHealthCheckService> _logger;
    private FreeTtsHealthResponse _lastCheckResult;
    private List<FreeTtsVoice> _cachedVoices = new();
    private DateTime _voicesCacheTime = DateTime.MinValue;
    private const int VoicesCacheDurationMinutes = 60;

    public FreeTtsHealthCheckService(IFreeTtsHttpClient httpClient, ILogger<FreeTtsHealthCheckService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _lastCheckResult = new FreeTtsHealthResponse
        {
            IsAvailable = false,
            Message = "Not checked yet",
            CheckedAt = DateTime.UtcNow
        };
    }

    public async Task<FreeTtsHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _httpClient.IsAvailableAsync(cancellationToken);

            _lastCheckResult = new FreeTtsHealthResponse
            {
                IsAvailable = isAvailable,
                Message = isAvailable ? "Service is available" : "Service is unavailable",
                CheckedAt = DateTime.UtcNow
            };

            if (isAvailable)
            {
                _logger.LogInformation("FreeTTS service health check passed");
                await RefreshVoicesCacheAsync(cancellationToken);
            }
            else
            {
                _logger.LogWarning("FreeTTS service health check failed");
            }

            return _lastCheckResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FreeTTS health check");

            _lastCheckResult = new FreeTtsHealthResponse
            {
                IsAvailable = false,
                Message = $"Health check failed: {ex.Message}",
                CheckedAt = DateTime.UtcNow
            };

            return _lastCheckResult;
        }
    }

    public FreeTtsHealthResponse GetLastCheckResult()
    {
        return _lastCheckResult;
    }

    public async Task<List<FreeTtsVoice>> GetCachedVoicesAsync(CancellationToken cancellationToken = default)
    {
        // Return cached voices if cache is still valid
        if (_cachedVoices.Any() && DateTime.UtcNow.Subtract(_voicesCacheTime).TotalMinutes < VoicesCacheDurationMinutes)
        {
            return _cachedVoices;
        }

        await RefreshVoicesCacheAsync(cancellationToken);
        return _cachedVoices;
    }

    private async Task RefreshVoicesCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetVoicesAsync(cancellationToken);

            if (response?.Data?.Voices != null)
            {
                _cachedVoices = response.Data.Voices;
                _voicesCacheTime = DateTime.UtcNow;
                _logger.LogInformation($"FreeTTS voices cache updated with {_cachedVoices.Count} voices");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh voices cache");
        }
    }
}
