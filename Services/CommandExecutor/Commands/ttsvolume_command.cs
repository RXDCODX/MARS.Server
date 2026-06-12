using System.Collections.Generic;
using System.Threading;
using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.Twitch.Synthesizer;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TtsVolumeCommand(TtsHubBroadcaster broadcaster) : BaseCommand
{
    public override string CommandName => "ttsvolume";
    public override string Description => "Установить громкость TTS";
    public override bool IsAdminCommand => true;

    public override CommandParameterInfo[] Parameters =>
        [
            new CommandParameterInfo()
            {
                Name = "volume",
                Description = "Громкость",
                Required = false,
                Type = "int",
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object>? parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = $"Текущая громкость TTS: {broadcaster.CurrentVolume * 100:0}%";

        if (parameters != null && parameters.TryGetValue("volume", out var v))
        {
            if (int.TryParse(v?.ToString(), out var value))
            {
                if (value is >= 0 and <= 200)
                {
                    var volume = value / 100.0;
                    var state = new TtsState { IsStopped = false, Volume = volume };
                    await broadcaster.BroadcastStateAsync(state, cancellationToken);
                    result = $"Громкость TTS установлена: {value}%";
                }
                else
                {
                    result = "Неверный параметр volume. Ожидается целое число 0..200";
                }
            }
            else
            {
                result = "Неверный параметр volume. Ожидается целое число 0..200";
            }
        }

        return result;
    }
}
