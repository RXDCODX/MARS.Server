using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Synthesizer;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TtsVolumeCommand(TtsHubBroadcaster broadcaster) : BaseCommand
{
    public override string CommandName => "ttsvolume";
    public override string Description => "Установить громкость TTS";
    public override bool IsAdminCommand => true;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object>? parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        if (parameters != null && parameters.TryGetValue("volume", out var v))
        {
            if (double.TryParse(v?.ToString(), out var vol))
            {
                vol = Math.Clamp(vol, 0.0, 1.0);
                var state = new TtsState { IsStopped = false, Volume = vol };
                await broadcaster.BroadcastStateAsync(state, cancellationToken);
                return $"Громкость TTS установлена: {vol}";
            }

            if (int.TryParse(v?.ToString(), out var value))
            {
                if (value is <= 100 and >= 0)
                {
                    var dblValue = Convert.ToDouble(value) / 100;
                    var state = new TtsState { IsStopped = false, Volume = dblValue };
                    await broadcaster.BroadcastStateAsync(state, cancellationToken);
                    return $"Громкость TTS установлена: {vol}";
                }
            }
        }

        return "Неверный параметр volume. Ожидается число 0.0..1.0";
    }
}
