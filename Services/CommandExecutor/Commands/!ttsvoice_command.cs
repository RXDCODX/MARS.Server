using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Synthesizer;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TtsVoiceCommand(ITtsHubBroadcaster broadcaster) : BaseCommand
{
    public override string CommandName => "ttsvoice";
    public override string Description => "Случайно сменить голос TTS";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось определить ваш идентификатор.";

        var userId = ResolveUserId(parameters);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            await broadcaster.BroadcastReassignVoiceAsync(userId, cancellationToken);
            result = "Голос случайно изменен";
        }

        return result;
    }

    private static string? ResolveUserId(Dictionary<string, object> parameters)
    {
        string? result = null;

        if (parameters.TryGetValue("user", out var userObj) && userObj is TwitchUser user)
        {
            result = user.TwitchId;
        }
        else if (
            parameters.TryGetValue("userId", out var userIdObj)
            && userIdObj is string userId
            && !string.IsNullOrWhiteSpace(userId)
        )
        {
            result = userId;
        }

        return result;
    }
}