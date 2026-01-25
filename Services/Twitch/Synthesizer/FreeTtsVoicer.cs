using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using MARS.Server.Services.Twitch.Synthesizer.FreeTts;
using MARS.Server.Services.Twitch.Synthesizer.FreeTts.Models;
using System.Text.Json;

namespace MARS.Server.Services.Twitch.Synthesizer;

/// <summary>
/// IVoicer implementation for FreeTTS online service
/// </summary>
public class FreeTtsVoicer : IVoicer
{
    private readonly IFreeTtsSynthesizerService _synthesizerService;
    private readonly ITtsVoiceRepository _voiceRepository;
    private readonly ILogger<IVoicer> _logger;
    private readonly IHostEnvironment _environment;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphore = new(1);
    private int _volume = 100;
    private HashSet<string> _blockedVoices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FreeTtsVoice> _voiceCache = new(
        StringComparer.OrdinalIgnoreCase
    );
    private string _selectedVoiceId = string.Empty;

    public bool IsActive { get; set; } = true;

    public FreeTtsVoicer(
        IFreeTtsSynthesizerService synthesizerService,
        ITtsVoiceRepository voiceRepository,
        ILogger<IVoicer> logger,
        IHostEnvironment environment,
        HttpClient httpClient
    )
    {
        _synthesizerService = synthesizerService;
        _voiceRepository = voiceRepository;
        _logger = logger;
        _environment = environment;
        _httpClient = httpClient;
        InitializeAsync().GetAwaiter().GetResult();
    }

    public int GetVolume()
    {
        return _volume;
    }

    public void ChangeVolume(int volume)
    {
        _volume = Math.Max(0, Math.Min(100, volume));
        _logger.LogInformation($"FreeTTS volume changed to {_volume}");
        
        // Send volume update to audio controller asynchronously
        _ = SetAudioVolumeAsync(_volume);
    }

    public async Task Sound(MessageToSynthezid message)
    {
        if (!IsActive)
        {
            _logger.LogWarning("FreeTTS voicer is not active");
            return;
        }

        if (string.IsNullOrWhiteSpace(message.Message))
        {
            _logger.LogWarning("Received empty message for synthesis");
            return;
        }

        // Check if message was already processed
        if (message.Guid != Guid.Empty && _synthesizerService.IsMessageProcessed(message.Guid.GetHashCode()))
        {
            _logger.LogInformation($"Message {message.Guid} was already processed, skipping synthesis");
            return;
        }

        try
        {
            await _semaphore.WaitAsync();

            // Select voice based on name
            var voiceId = await GetVoiceIdByNameAsync(message.Name) ?? _selectedVoiceId;
            if (string.IsNullOrWhiteSpace(voiceId))
            {
                _logger.LogWarning("No voice ID available for synthesis");
                return;
            }

            // Check if voice is blocked
            if (_blockedVoices.Contains(voiceId))
            {
                _logger.LogWarning($"Voice {voiceId} is blocked");
                return;
            }

            _logger.LogInformation(
                $"Starting synthesis: '{message.Message}' with voice: {voiceId}"
            );

            // Use message GUID hash as message ID for tracking
            long? messageId = message.Guid != Guid.Empty ? message.Guid.GetHashCode() : null;
            var audioUrl = await _synthesizerService.SynthesizeAsync(message.Message, voiceId, messageId);

            if (!string.IsNullOrEmpty(audioUrl))
            {
                _logger.LogInformation($"Synthesis successful: {audioUrl}");
                
                // Queue audio for playback
                await QueueAudioPlaybackAsync(audioUrl, message.Name);
                
                // Set audio volume
                await SetAudioVolumeAsync(_volume);
            }
            else
            {
                _logger.LogError("Synthesis failed - no audio URL returned");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FreeTTS synthesis");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task Stop()
    {
        IsActive = false;
        _logger.LogInformation("FreeTTS voicer stopped");
        
        // Stop audio playback on AudioController
        await StopAudioPlaybackAsync();
    }

    public async Task Block()
    {
        IsActive = false;
        _logger.LogInformation("FreeTTS voicer blocked");
        
        // Stop audio playback on AudioController
        await StopAudioPlaybackAsync();
    }

    public Task Unblock()
    {
        IsActive = true;
        _logger.LogInformation("FreeTTS voicer unblocked");
        return Task.CompletedTask;
    }

    public async Task RefreshBlockedVoicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            var blockedNames = await _voiceRepository.GetBlockedVoicesAsync(cancellationToken);
            _blockedVoices = new HashSet<string>(blockedNames, StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation($"Blocked voices refreshed: {_blockedVoices.Count} voices");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ResetVoiceAsync(string name, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _voiceCache.Remove(name);
            _logger.LogInformation($"Voice cache reset for: {name}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ResetAllVoicesAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _voiceCache.Clear();
            _logger.LogInformation("All voice cache cleared");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetLinkedVoicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var voices = await _synthesizerService.GetAvailableVoicesAsync(cancellationToken);
        var linked = voices
            .Where(v => !_blockedVoices.Contains(v.Id))
            .ToDictionary(v => v.Id, v => v.Name, StringComparer.OrdinalIgnoreCase);

        return linked;
    }

    public async Task<List<string>> GetInstalledVoicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var voices = await _synthesizerService.GetAvailableVoicesAsync(cancellationToken);
        return voices.Where(v => !_blockedVoices.Contains(v.Id)).Select(v => v.Name).ToList();
    }

    /// <summary>
    /// Queues audio for playback on the audio controller
    /// </summary>
    private async Task QueueAudioPlaybackAsync(string audioUrl, string displayName)
    {
        try
        {
            var audioControllerUrl = GetAudioControllerUrl();
            var endpoint = $"{audioControllerUrl}/api/audioplayback/queue";

            var payload = new
            {
                audioUrl = audioUrl,
                displayName = displayName,
                priority = 50
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            _logger.LogInformation($"Queueing audio for playback: {endpoint}");
            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Audio queued successfully: {responseContent}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to queue audio playback: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing audio for playback");
        }
    }

    /// <summary>
    /// Stops audio playback on the audio controller
    /// </summary>
    private async Task StopAudioPlaybackAsync()
    {
        try
        {
            var audioControllerUrl = GetAudioControllerUrl();
            var endpoint = $"{audioControllerUrl}/api/audioplayback/stop";

            _logger.LogInformation($"Stopping audio playback: {endpoint}");
            var response = await _httpClient.PostAsync(endpoint, null);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Audio playback stopped successfully");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Failed to stop audio playback: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping audio playback");
        }
    }

    /// <summary>
    /// Sets volume on the audio controller
    /// </summary>
    private async Task SetAudioVolumeAsync(int volume)
    {
        try
        {
            var audioControllerUrl = GetAudioControllerUrl();
            var endpoint = $"{audioControllerUrl}/api/audioplayback/volume?volume={volume}";

            _logger.LogInformation($"Setting audio volume to {volume}%: {endpoint}");
            var response = await _httpClient.PostAsync(endpoint, null);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Audio volume set successfully: {responseContent}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Failed to set audio volume: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting audio volume");
        }
    }

    /// <summary>
    /// Gets the audio controller URL based on environment
    /// </summary>
    private string GetAudioControllerUrl()
    {
        return _environment.IsProduction() ? "http://localhost:30695" : "http://localhost:30691";
    }

    private async Task<string?> GetVoiceIdByNameAsync(string voiceName)
    {
        if (string.IsNullOrWhiteSpace(voiceName))
        {
            return null;
        }

        // Check cache first
        if (_voiceCache.TryGetValue(voiceName, out var cachedVoice))
        {
            return cachedVoice.Id;
        }

        // Try to find voice by name
        var voice = await _synthesizerService.FindVoiceByNameAsync(voiceName);
        if (voice != null)
        {
            _voiceCache.TryAdd(voiceName, voice);
            return voice.Id;
        }

        return null;
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Check service availability
            var isAvailable = await _synthesizerService.IsAvailableAsync();
            if (!isAvailable)
            {
                _logger.LogWarning("FreeTTS service is not available during initialization");
                IsActive = false;
                return;
            }

            // Load cached voices
            var voices = await _synthesizerService.GetAvailableVoicesAsync();
            foreach (var voice in voices)
            {
                _voiceCache.TryAdd(voice.Id, voice);
            }

            _logger.LogInformation($"FreeTTS voicer initialized with {voices.Count} voices");

            // Load blocked voices
            await RefreshBlockedVoicesAsync();

            // Select default voice (first Russian male voice or just first available)
            var defaultVoice =
                voices.FirstOrDefault(v => v.Lang == "ru-RU" && v.Sex == "m")
                ?? voices.FirstOrDefault();

            if (defaultVoice != null)
            {
                _selectedVoiceId = defaultVoice.Id;
                _logger.LogInformation(
                    $"Default voice selected: {defaultVoice.Name} ({_selectedVoiceId})"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FreeTTS voicer initialization");
            IsActive = false;
        }
    }
}
