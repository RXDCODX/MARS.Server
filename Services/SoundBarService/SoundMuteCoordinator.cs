using System;
using System.Threading.Tasks;
using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.SoundBarService.Entitys;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.Synthesizer;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.SoundBarService;

public class SoundMuteCoordinator
{
    private readonly StateManager _stateManager;
    private readonly ITtsHubBroadcaster _ttsHubBroadcaster;
    private readonly ILogger<SoundMuteCoordinator> _logger;
    private readonly ISoundBar _soundBar;

    // Сохраняем предыдущую громкость TTS, чтобы восстановить после unmute
    private double? _previousTtsVolume;
    private bool _isMuted = false;

    public SoundMuteCoordinator(
        ISoundBar soundBar,
        StateManager stateManager,
        ITtsHubBroadcaster ttsHubBroadcaster,
        ILogger<SoundMuteCoordinator> logger
    )
    {
        _soundBar = soundBar;
        _stateManager = stateManager;
        _ttsHubBroadcaster = ttsHubBroadcaster;
        _logger = logger;
    }

    public async Task MuteAsync(params string[] args)
    {
        try
        {
            await _soundBar.Mute(args);
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

            if (!_isMuted)
            {
                _previousTtsVolume = _ttsHubBroadcaster.CurrentVolume;
                var ttsState = new TtsState { IsStopped = true, Volume = 0.0 };
                await _ttsHubBroadcaster.BroadcastStateAsync(ttsState);
                _isMuted = true;
            }
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
            await _soundBar.Unmute();
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
            _isMuted = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to coordinate unmute actions");
        }
    }
}
