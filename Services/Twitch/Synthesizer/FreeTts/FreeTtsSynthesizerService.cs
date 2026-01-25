using System.Collections.Concurrent;
using MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;
using MARS.Server.Services.Twitch.Synthesizer.TextProcessing;

namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts;

public interface IFreeTtsSynthesizerService
{
    /// <summary>
    /// Synthesizes text using FreeTTS service
    /// </summary>
    /// <param name="text">Text to synthesize</param>
    /// <param name="voiceId">FreeTTS voice ID</param>
    /// <param name="messageId">Optional message ID for tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>URL to audio file or null if synthesis failed</returns>
    Task<string?> SynthesizeAsync(
        string text,
        string voiceId,
        long? messageId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Synthesizes text and returns audio file bytes
    /// </summary>
    Task<byte[]?> SynthesizeAndGetAudioAsync(
        string text,
        string voiceId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all available voices
    /// </summary>
    Task<List<FreeTtsVoice>> GetAvailableVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a voice by name (supports partial matching)
    /// </summary>
    Task<FreeTtsVoice?> FindVoiceByNameAsync(
        string voiceName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets voice by language code
    /// </summary>
    Task<List<FreeTtsVoice>> GetVoicesByLanguageAsync(
        string languageCode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if service is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if message was already processed
    /// </summary>
    bool IsMessageProcessed(long messageId);

    /// <summary>
    /// Gets count of processed messages
    /// </summary>
    int GetProcessedMessageCount();
}

public class FreeTtsSynthesizerService : IFreeTtsSynthesizerService
{
    private readonly IFreeTtsHttpClient _httpClient;
    private readonly IFreeTtsHealthCheckService _healthCheckService;
    private readonly ITextNormalizationService _textNormalizationService;
    private readonly ILogger<FreeTtsSynthesizerService> _logger;
    private bool _isServiceAvailable = false;

    // Track processed messages: messageId -> (audioUrl, timestamp)
    private readonly ConcurrentDictionary<
        long,
        (string AudioUrl, DateTime ProcessedAt)
    > _processedMessages = new();

    public FreeTtsSynthesizerService(
        IFreeTtsHttpClient httpClient,
        IFreeTtsHealthCheckService healthCheckService,
        ITextNormalizationService textNormalizationService,
        ILogger<FreeTtsSynthesizerService> logger
    )
    {
        _httpClient = httpClient;
        _healthCheckService = healthCheckService;
        _textNormalizationService = textNormalizationService;
        _logger = logger;
    }

    public async Task<string?> SynthesizeAsync(
        string text,
        string voiceId,
        long? messageId = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!await EnsureServiceAvailableAsync(cancellationToken))
        {
            return null;
        }

        // Check if message was already processed
        if (
            messageId.HasValue
            && _processedMessages.TryGetValue(messageId.Value, out var cachedResult)
        )
        {
            _logger.LogInformation(
                $"Message {messageId} was already processed. Using cached audio URL"
            );
            return cachedResult.AudioUrl;
        }

        var normalizedText = NormalizeText(text);

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            _logger.LogWarning("Text became empty after normalization");
            return null;
        }

        try
        {
            // Add invisible separator at the beginning (U+2063)
            var textWithMarker = "\u2063" + normalizedText;

            _logger.LogInformation(
                $"Synthesizing text: '{textWithMarker}' with voice: {voiceId}"
                    + (messageId.HasValue ? $" (messageId: {messageId})" : "")
            );
            _logger.LogDebug(
                $"Raw text bytes: {BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(textWithMarker))}"
            );

            var response = await _httpClient.SynthesizeAsync(
                textWithMarker,
                voiceId,
                cancellationToken
            );

            if (response?.Status == "pending" || response?.Status == "success")
            {
                // Get history to retrieve the URL
                await Task.Delay(1000, cancellationToken); // Wait for processing
                var history = await _httpClient.GetHistoryAsync(cancellationToken);

                if (history?.Data?.Count > 0)
                {
                    var lastItem = history.Data.Last();
                    if (lastItem.Status == "done")
                    {
                        _logger.LogInformation($"Synthesis successful, audio URL: {lastItem.Url}");

                        // Cache the result if messageId is provided
                        if (messageId.HasValue)
                        {
                            _processedMessages.TryAdd(
                                messageId.Value,
                                (lastItem.Url, DateTime.UtcNow)
                            );
                            _logger.LogInformation(
                                $"Cached synthesis for message {messageId}. Total cached: {_processedMessages.Count}"
                            );
                        }

                        return lastItem.Url;
                    }
                }
            }

            _logger.LogWarning($"Synthesis failed with status: {response?.Status}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during text synthesis");
            return null;
        }
    }

    public async Task<byte[]?> SynthesizeAndGetAudioAsync(
        string text,
        string voiceId,
        CancellationToken cancellationToken = default
    )
    {
        var audioUrl = await SynthesizeAsync(text, voiceId, cancellationToken: cancellationToken);

        if (string.IsNullOrEmpty(audioUrl))
        {
            return null;
        }

        try
        {
            using var httpClient = new HttpClient();
            var audioBytes = await httpClient.GetByteArrayAsync(audioUrl, cancellationToken);
            return audioBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to download audio from {audioUrl}");
            return null;
        }
    }

    public async Task<List<FreeTtsVoice>> GetAvailableVoicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!await EnsureServiceAvailableAsync(cancellationToken))
        {
            return new List<FreeTtsVoice>();
        }

        return await _healthCheckService.GetCachedVoicesAsync(cancellationToken);
    }

    public async Task<FreeTtsVoice?> FindVoiceByNameAsync(
        string voiceName,
        CancellationToken cancellationToken = default
    )
    {
        var voices = await GetAvailableVoicesAsync(cancellationToken);

        return voices.FirstOrDefault(v =>
                v.Name.Equals(voiceName, StringComparison.OrdinalIgnoreCase)
            )
            ?? voices.FirstOrDefault(v =>
                v.Name.Contains(voiceName, StringComparison.OrdinalIgnoreCase)
            );
    }

    public async Task<List<FreeTtsVoice>> GetVoicesByLanguageAsync(
        string languageCode,
        CancellationToken cancellationToken = default
    )
    {
        var voices = await GetAvailableVoicesAsync(cancellationToken);

        return voices
            .Where(v => v.Lang.StartsWith(languageCode, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var health = await _healthCheckService.CheckHealthAsync(cancellationToken);
        _isServiceAvailable = health.IsAvailable;
        return _isServiceAvailable;
    }

    public bool IsMessageProcessed(long messageId)
    {
        return _processedMessages.ContainsKey(messageId);
    }

    public int GetProcessedMessageCount()
    {
        return _processedMessages.Count;
    }

    private async Task<bool> EnsureServiceAvailableAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_isServiceAvailable)
        {
            _isServiceAvailable = await IsAvailableAsync(cancellationToken);
        }

        return _isServiceAvailable;
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // First, replace non-Cyrillic characters with Cyrillic equivalents
        var normalized = _textNormalizationService.Normalize(text, replaceMode: true);

        // Remove extra whitespace
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }
}
