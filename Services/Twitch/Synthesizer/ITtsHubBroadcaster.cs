using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.Twitch.Synthesizer;

public interface ITtsHubBroadcaster
{
    double CurrentVolume { get; }

    bool CurrentRelayToDiscord { get; }

    Task BroadcastAsync(
        TwitchUser? user,
        string message,
        CancellationToken cancellationToken = default
    );

    Task BroadcastStateAsync(TtsState? state, CancellationToken cancellationToken = default);

    Task BroadcastReassignVoiceAsync(string userId, CancellationToken cancellationToken = default);
}
