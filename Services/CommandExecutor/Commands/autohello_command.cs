using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.WaifuRoll;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class AutoHelloCommand(WaifuRollService waifuRollService) : BaseCommand
{
    public override string CommandName => "autohello";
    public override string Description => "Включить или выключить приветствие от супруга(и)";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось определить вашего пользователя.";

        var userId = ResolveUserId(parameters);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var enabled = await waifuRollService.ToggleAutoHelloAsync(userId, cancellationToken);

            result = enabled
                ? "Приветствие от супруга(и) включено."
                : "Приветствие от супруга(и) выключено.";
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
