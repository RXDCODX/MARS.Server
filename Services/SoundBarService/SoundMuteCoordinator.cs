using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.SoundBarService.Entitys;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.Synthesizer;

namespace MARS.Server.Services.SoundBarService;

public class SoundMuteCoordinator(
    ISoundBar soundBar,
    StateManager stateManager,
    ITtsHubBroadcaster ttsHubBroadcaster,
    ILogger<SoundMuteCoordinator> logger
)
{
    // Сохраняем предыдущую громкость TTS, чтобы восстановить после unmute
    private double? _previousTtsVolume;
    private bool _isMuted = false;

    public async Task MuteAsync(params string[] args)
    {
        try
        {
            await soundBar.Mute(args);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to call SoundBar.Mute");
        }

        try
        {
            var state = await stateManager.GetStateAsync();
            if (state.State == PlaybackState.Playing)
            {
                await stateManager.SetPausedAsync(true);
                await stateManager.SetPausedByMuteAsync(true);
            }

            await stateManager.SetMutedAsync(true);

            // Сохраняем текущую громкость TTS и отправляем Volume=0

            if (!_isMuted)
            {
                _previousTtsVolume = ttsHubBroadcaster.CurrentVolume;
                var ttsState = new TtsState { IsStopped = true, Volume = 0.0 };
                await ttsHubBroadcaster.BroadcastStateAsync(ttsState);
                _isMuted = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to coordinate mute actions");
        }
    }

    public async Task UnmuteAsync()
    {
        try
        {
            await soundBar.Unmute();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to call SoundBar.Unmute");
        }

        try
        {
            await stateManager.SetMutedAsync(false);

            var state = await stateManager.GetStateAsync();
            if (state.PausedByMute)
            {
                await stateManager.SetPausedAsync(false);
                await stateManager.SetPausedByMuteAsync(false);
            }

            var volumeToRestore = _previousTtsVolume ?? ttsHubBroadcaster.CurrentVolume;
            var ttsState = new TtsState { IsStopped = false, Volume = volumeToRestore };
            await ttsHubBroadcaster.BroadcastStateAsync(ttsState);
            _isMuted = false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to coordinate unmute actions");
        }
    }
}
