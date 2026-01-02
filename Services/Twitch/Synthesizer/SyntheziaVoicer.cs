using System.Globalization;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Text;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.Twitch.Synthesizer;

[SupportedOSPlatform("windows")]
public class SyntheziaVoicer : IVoicer
{
    private readonly Dictionary<string, InstalledVoice> _linkedVoices = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ILogger<IVoicer> _logger;
    private readonly ITtsVoiceRepository _voiceRepository;
    private readonly SpeechSynthesizer _speechSynthesizer = new();
    private readonly SemaphoreSlim _semaphore = new(1);
    private HashSet<string> _blockedVoices = new(StringComparer.OrdinalIgnoreCase);

    public bool IsActive { get; set; } = true;

    public SyntheziaVoicer(ILogger<IVoicer> logger, ITtsVoiceRepository voiceRepository)
    {
        _logger = logger;
        _voiceRepository = voiceRepository;
        if (OperatingSystem.IsWindows())
        {
            _speechSynthesizer.SetOutputToDefaultAudioDevice();
            try
            {
                RefreshBlockedVoicesAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to preload blocked voices list");
            }
        }
    }

    public int GetVolume()
    {
        return _speechSynthesizer.Volume;
    }

    public void ChangeVolume(int volume)
    {
        if (OperatingSystem.IsWindows())
        {
            _speechSynthesizer.Volume = volume;
        }
    }

    public async Task RefreshBlockedVoicesAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var blocked = await _voiceRepository.GetBlockedVoicesAsync(cancellationToken);
        _blockedVoices = new HashSet<string>(blocked, StringComparer.OrdinalIgnoreCase);
    }

    public async Task ResetVoiceAsync(string name, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _linkedVoices.Remove(name);
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
            _linkedVoices.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<IReadOnlyDictionary<string, string>> GetLinkedVoicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyDictionary<string, string> result = _linkedVoices.ToDictionary(
            x => x.Key,
            x => x.Value.VoiceInfo.Name,
            StringComparer.OrdinalIgnoreCase
        );
        return Task.FromResult(result);
    }

    public Task<List<string>> GetInstalledVoicesAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new List<string>());
        }

        var voices = _speechSynthesizer
            .GetInstalledVoices(new CultureInfo("ru-RU"))
            .Select(v => v.VoiceInfo.Name)
            .ToList();
        return Task.FromResult(voices);
    }

    public async Task Sound(MessageToSynthezid message)
    {
        if (!OperatingSystem.IsWindows() || !IsActive)
        {
            return;
        }

        try
        {
            var preparedText = PrepareText(message.Message);

            await _semaphore.WaitAsync();
            try
            {
                var hasVoice = _linkedVoices.TryGetValue(message.Name, out var voice);

                if (hasVoice && voice is not null && IsVoiceBlocked(voice))
                {
                    _linkedVoices.Remove(message.Name);
                    hasVoice = false;
                    voice = null;
                }

                if (hasVoice && voice is not null)
                {
                    SpeakWithVoice(voice, preparedText);
                }
                else
                {
                    var randomVoice = GetRandomAllowedVoice();
                    if (randomVoice is null)
                    {
                        _logger.LogWarning(
                            "No available voices to assign (all blocked or missing)."
                        );
                        return;
                    }

                    _linkedVoices[message.Name] = randomVoice;
                    SpeakWithVoice(
                        randomVoice,
                        $"Привет, {message.Name}! Для тебя был выбран голос {randomVoice.VoiceInfo.Name}",
                        preparedText
                    );
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
        }
    }

    public Task Stop()
    {
        if (OperatingSystem.IsWindows())
        {
            _speechSynthesizer.SpeakAsyncCancelAll();
        }

        return Task.CompletedTask;
    }

    public async Task Block()
    {
        IsActive = false;
        await Stop();
    }

    public Task Unblock()
    {
        IsActive = true;
        return Task.CompletedTask;
    }

    private string PrepareText(string input)
    {
        var sb = new StringBuilder();

        foreach (var word in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Uri.TryCreate(word, UriKind.Absolute, out var result))
            {
                sb.Append(result.Host);
            }
            else
            {
                sb.Append(word);
            }

            sb.Append(' ');
        }

        return sb.ToString();
    }

    private void SpeakWithVoice(InstalledVoice voice, string text, string? additional = null)
    {
        var builder = new PromptBuilder();
        builder.StartVoice(voice.VoiceInfo.Name);
        builder.AppendText(text);
        if (!string.IsNullOrWhiteSpace(additional))
        {
            builder.AppendBreak(TimeSpan.FromSeconds(1));
            builder.AppendText(additional);
        }
        builder.EndVoice();
        _speechSynthesizer.SpeakAsync(builder);
    }

    private InstalledVoice? GetRandomAllowedVoice()
    {
        var voices = _speechSynthesizer
            .GetInstalledVoices(new CultureInfo("ru-RU"))
            .Where(v => !IsVoiceBlocked(v))
            .ToList();

        if (voices.Count == 0)
        {
            return null;
        }

        var index = Random.Shared.Next(voices.Count);
        return voices[index];
    }

    private bool IsVoiceBlocked(InstalledVoice voice)
    {
        return _blockedVoices.Contains(NormalizeVoiceName(voice.VoiceInfo.Name));
    }

    private static string NormalizeVoiceName(string voiceName)
    {
        return voiceName.Trim().ToLowerInvariant();
    }
}
