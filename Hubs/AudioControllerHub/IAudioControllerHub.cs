using System.Threading.Tasks;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Hubs.AudioControllerHub;

using Models.VoiceRecognition;

/// <summary>
/// Server-to-client interface for AudioController hub.
/// Defines methods that MARS.Server can invoke on AudioController.
/// </summary>
public interface IAudioControllerHub
{
    // ── SoundBar commands ──

    Task MuteProcesses(string correlationId, string[] processNames);

    Task UnmuteProcesses(string correlationId);

    Task GetBagCount(string correlationId);

    // ── OBS commands ──

    Task ConnectObs(string correlationId);

    Task DisconnectObs(string correlationId);

    Task ScreenshotObs(string correlationId, string? sourceName);

    Task FreezeObs(string correlationId);

    Task UnfreezeObs(string correlationId);

    Task SwitchToPauseScene(string correlationId);

    Task SwitchFromPauseScene(string correlationId);

    Task TogglePauseObs(string correlationId, int mode);

    Task GetObsStatus(string correlationId);

    // ── TTS commands (migrated from VoiceRecognitionHub) ──

    Task PlayTts(TwitchUser user, string message);

    Task UpdateTtsState(TtsState state);

    Task ReassignVoice(string userId);

    // ── Health ──

    Task Ping(string correlationId);
}
