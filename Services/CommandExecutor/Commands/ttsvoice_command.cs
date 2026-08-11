using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Synthesizer;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TtsVoiceCommand(ITtsHubBroadcaster broadcaster) : BaseCommand
{
    public override string CommandName => "ttsvoice";
    public override string Description => "Случайно сменить голос TTS";
    public override bool IsAdminCommand => false;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        if (!parameters.TryGetValue("userId", out var userIdObj) || userIdObj is not string userId)
        {
            return "Не удалось определить ваш идентификатор.";
        }

        await broadcaster.BroadcastReassignVoiceAsync(userId, cancellationToken);

        return "Голос случайно изменен";
    }
}
