using System.Net.Http.Json;
using System.Text.Json;
using MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;

namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts;

/// <summary>
/// Custom JSON naming policy that converts property names to lowercase
/// </summary>
public class LowercaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        return name.ToLowerInvariant();
    }
}

public interface IFreeTtsHttpClient
{
    /// <summary>
    /// Gets available voices from FreeTTS API
    /// </summary>
    Task<FreeTtsListResponse?> GetVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests text synthesis
    /// </summary>
    Task<FreeTtsSynthesisResponse?> SynthesizeAsync(
        string text,
        string voiceId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets synthesis history
    /// </summary>
    Task<FreeTtsHistoryResponse?> GetHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if FreeTTS service is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates and sets a random UID cookie
    /// </summary>
    void GenerateRandomUid();
}

public class FreeTtsHttpClient : IFreeTtsHttpClient
{
    private const string BaseUrl = "https://freetts.ru/api/";
    private const string UidCookieName = "uid";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = new LowercaseNamingPolicy(),
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<FreeTtsHttpClient> _logger;
    private string _currentUid = string.Empty;

    public FreeTtsHttpClient(HttpClient httpClient, ILogger<FreeTtsHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(BaseUrl);

        // Set headers как в JavaScript fetch запросе
        _httpClient.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:146.0) Gecko/20100101 Firefox/146.0"
        );
        _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        _httpClient.DefaultRequestHeaders.Add(
            "Accept-Language",
            "ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3"
        );
        _httpClient.DefaultRequestHeaders.Add("Sec-GPC", "1");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
        _httpClient.DefaultRequestHeaders.Add("Priority", "u=0");
        _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
        _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

        GenerateRandomUid();
    }

    public async Task<FreeTtsListResponse?> GetVoicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            SetUidCookie();
            var response = await _httpClient.GetAsync("list", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<FreeTtsListResponse>(
                JsonOptions,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get voices from FreeTTS");
            return null;
        }
    }

    public async Task<FreeTtsSynthesisResponse?> SynthesizeAsync(
        string text,
        string voiceId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(voiceId))
            {
                _logger.LogWarning("Invalid synthesis request: text or voiceId is empty");
                return null;
            }

            SetUidCookie();

            var request = new FreeTtsSynthesisRequest
            {
                Text = text,
                VoiceId = voiceId,
                Ext = "mp3",
            };

            using var content = JsonContent.Create(request, options: JsonOptions);

            // Log the request body
            var requestBodyJson = await content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation($"Sending synthesis request: {requestBodyJson}");
            _logger.LogInformation($"Text length: {text.Length}, VoiceId: {voiceId}");

            // Явно установить Content-Type заголовок
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/json"
            );

            var response = await _httpClient.PostAsync("synthesis", content, cancellationToken);

            // Log response
            var responseBodyJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation($"Synthesis response status: {response.StatusCode}");
            _logger.LogInformation($"Synthesis response body: {responseBodyJson}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    $"Synthesis request failed with status {response.StatusCode}: {responseBodyJson}"
                );
                return null;
            }

            var parsedResponse = JsonSerializer.Deserialize<FreeTtsSynthesisResponse>(
                responseBodyJson,
                JsonOptions
            );

            _logger.LogInformation($"Parsed response status: {parsedResponse?.Status}");

            return parsedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synthesize text with FreeTTS");
            return null;
        }
    }

    public async Task<FreeTtsHistoryResponse?> GetHistoryAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            SetUidCookie();
            var response = await _httpClient.GetAsync("history", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<FreeTtsHistoryResponse>(
                JsonOptions,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get history from FreeTTS");
            return null;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SetUidCookie();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _httpClient.GetAsync("list", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FreeTTS service is not available");
            return false;
        }
    }

    public void GenerateRandomUid()
    {
        _currentUid = Guid.NewGuid().ToString("N").Substring(0, 32);
        _logger.LogDebug($"Generated new UID for FreeTTS: {_currentUid}");
    }

    private void SetUidCookie()
    {
        // Remove existing Cookie header if present
        if (_httpClient.DefaultRequestHeaders.Contains("Cookie"))
        {
            _httpClient.DefaultRequestHeaders.Remove("Cookie");
        }

        // Add new Cookie header
        _httpClient.DefaultRequestHeaders.Add("Cookie", $"{UidCookieName}={_currentUid}");
    }
}
