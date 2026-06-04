using MARS.Server.Hubs.Models.VoiceRecognition;

namespace MARS.Server.Services.Twitch.Synthesizer;

public interface ITtsHubBroadcaster
{
    double CurrentVolume { get; }

    Task BroadcastAsync(
        TwitchUser? user,
        string message,
        CancellationToken cancellationToken = default
    );

    Task BroadcastStateAsync(TtsState? state, CancellationToken cancellationToken = default);
}
