using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using MARS.Server.Services.Discord.TtsVoiceRelay;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using TwitchUserModel = MARS.Server.Services.Twitch.Entitys.TwitchUser;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class SyntheziaVoicer : IVoicer
{
    private readonly ILogger<IVoicer> _logger;
    private readonly TtsHubBroadcaster? _ttsHubBroadcaster;

    public bool IsActive { get; set; } = true;

    public SyntheziaVoicer(
        ILogger<IVoicer> logger,
        TtsHubBroadcaster ttsHubBroadcaster,
        IServiceProvider serviceProvider
    )
    {
        _logger = logger;
        _ttsHubBroadcaster = ttsHubBroadcaster;
    }

    public SyntheziaVoicer(
        ILogger<IVoicer> logger,
        ITtsVoiceRepository voiceRepository,
        IServiceProvider serviceProvider
    )
    {
        _logger = logger;
        _ttsHubBroadcaster = serviceProvider.GetService<TtsHubBroadcaster>();
        _ = voiceRepository;
    }

    public SyntheziaVoicer(
        ILogger<IVoicer> logger,
        ITtsVoiceRepository voiceRepository,
        TtsHubBroadcaster ttsHubBroadcaster,
        IServiceProvider serviceProvider
    ) : this(logger, ttsHubBroadcaster, serviceProvider)
    {
        _ = voiceRepository;
    }

    public int GetVolume()
    {
        return 100;
    }

    public void ChangeVolume(int volume)
    {
        // Volume is controlled in AudioController now.
    }

    public Task RefreshBlockedVoicesAsync(CancellationToken cancellationToken = default)
    {
        // Moved to AudioController. Server no longer tracks blocked voices.
        return Task.CompletedTask;
    }

    public Task ResetVoiceAsync(string name, CancellationToken cancellationToken = default)
    {
        // Voice mapping moved to AudioController
        return Task.CompletedTask;
    }

    public Task ResetAllVoicesAsync(CancellationToken cancellationToken = default)
    {
        // Voice mapping moved to AudioController
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, string>> GetLinkedVoicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        // No server-side linked voices after migration
        return Task.FromResult((IReadOnlyDictionary<string, string>)new Dictionary<string, string>());
    }

    public Task<List<string>> GetInstalledVoicesAsync(CancellationToken cancellationToken = default)
    {
        // Installed/system voices are not used by AudioController TTS engine
        return Task.FromResult(new List<string>());
    }

    public async Task Sound(TwitchUserModel twitchUser, string message)
    {
        if (!IsActive)
        {
            return;
        }

        try
        {
            if (_ttsHubBroadcaster is not null)
            {
                await _ttsHubBroadcaster.BroadcastAsync(twitchUser, PrepareText(message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
        }
    }

    public Task Sound(MessageToSynthezid message)
    {
        if (!IsActive)
        {
            return Task.CompletedTask;
        }

        var twitchUser = new TwitchUserModel
        {
            TwitchId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            UserLogin = message.Name,
            DisplayName = message.Name,
            IsModerator = false,
            IsVip = false,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
        };

        return Sound(twitchUser, message.Message);
    }

    public Task Stop()
    {
        // No local synthesizer to stop after migration.
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

    private static string PrepareText(string input)
    {
        var sb = new StringBuilder();

        foreach (var word in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            sb.Append(Uri.TryCreate(word, UriKind.Absolute, out var result) ? result.Host : word);

            sb.Append(' ');
        }

        return sb.ToString();
    }

    private async Task SpeakWithVoice(string text, string? additional = null)
    {
        var twitchUser = new TwitchUserModel
        {
            TwitchId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            UserLogin = "Unknown",
            DisplayName = "Unknown",
            IsModerator = false,
            IsVip = false,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
        };

        if (_ttsHubBroadcaster is not null)
        {
            await _ttsHubBroadcaster.BroadcastAsync(twitchUser, text);
        }
    }

    private static string NormalizeVoiceName(string voiceName)
    {
        return voiceName.Trim();
    }
}
