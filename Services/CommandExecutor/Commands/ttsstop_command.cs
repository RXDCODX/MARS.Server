using System.Collections.Generic;
using System.Threading;
using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.Twitch.Synthesizer;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TtsStopCommand(TtsHubBroadcaster broadcaster) : BaseCommand
{
    public override string CommandName => "ttsstop";
    public override string Description => "Остановить воспроизведение TTS";
    public override bool IsAdminCommand => true;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var state = new TtsState { IsStopped = true, Volume = 0.0 };
        await broadcaster.BroadcastStateAsync(state, cancellationToken);
        return "TTS остановлен";
    }
}
