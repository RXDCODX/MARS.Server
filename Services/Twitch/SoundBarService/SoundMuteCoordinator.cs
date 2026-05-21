using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.SoundBarService.Entitys;
using MARS.Server.Services.Twitch.Synthesizer;

namespace MARS.Server.Services.Twitch.SoundBarService;

public class SoundMuteCoordinator
{
    private readonly SoundBarFactory? _soundBarFactory;
    private readonly StateManager _stateManager;
    private readonly ITtsHubBroadcaster _ttsHubBroadcaster;
    private readonly ILogger<SoundMuteCoordinator> _logger;
    private readonly Func<ISoundBar>? _soundBarProvider;

    // Сохраняем предыдущую громкость TTS, чтобы восстановить после unmute
    private double? _previousTtsVolume;

    public SoundMuteCoordinator(
        Func<ISoundBar> soundBarProvider,
        StateManager stateManager,
        ITtsHubBroadcaster ttsHubBroadcaster,
        ILogger<SoundMuteCoordinator> logger
    )
    {
        _soundBarProvider = soundBarProvider;
        _stateManager = stateManager;
        _ttsHubBroadcaster = ttsHubBroadcaster;
        _logger = logger;
    }

    public SoundMuteCoordinator(
        SoundBarFactory soundBarFactory,
        StateManager stateManager,
        ITtsHubBroadcaster ttsHubBroadcaster,
        ILogger<SoundMuteCoordinator> logger
    )
    {
        _soundBarFactory = soundBarFactory;
        _stateManager = stateManager;
        _ttsHubBroadcaster = ttsHubBroadcaster;
        _logger = logger;
    }

    public async Task MuteAsync(params string[] args)
    {
        try
        {
            var sb = _soundBarProvider?.Invoke() ?? _soundBarFactory!.CreateSoundBar();
            await sb.Mute(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call SoundBar.Mute");
        }

        try
        {
            var state = await _stateManager.GetStateAsync();
            if (state.State == PlaybackState.Playing)
            {
                await _stateManager.SetPausedAsync(true);
                await _stateManager.SetPausedByMuteAsync(true);
            }

            await _stateManager.SetMutedAsync(true);

            // Сохраняем текущую громкость TTS и отправляем Volume=0
            _previousTtsVolume = _ttsHubBroadcaster.CurrentVolume;
            var ttsState = new TtsState { IsStopped = false, Volume = 0.0 };
            await _ttsHubBroadcaster.BroadcastStateAsync(ttsState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to coordinate mute actions");
        }
    }

    public async Task UnmuteAsync()
    {
        try
        {
            var sb = _soundBarProvider?.Invoke() ?? _soundBarFactory!.CreateSoundBar();
            await sb.Unmute();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call SoundBar.Unmute");
        }

        try
        {
            await _stateManager.SetMutedAsync(false);

            var state = await _stateManager.GetStateAsync();
            if (state.PausedByMute)
            {
                await _stateManager.SetPausedAsync(false);
                await _stateManager.SetPausedByMuteAsync(false);
            }

            var volumeToRestore = _previousTtsVolume ?? _ttsHubBroadcaster.CurrentVolume;
            var ttsState = new TtsState { IsStopped = false, Volume = volumeToRestore };
            await _ttsHubBroadcaster.BroadcastStateAsync(ttsState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to coordinate unmute actions");
        }
    }
}
