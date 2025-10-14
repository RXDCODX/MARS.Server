using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class WrongCommand(CommandsService commandsService) : BaseCommand
{
    public override string CommandName => "wrong";
    public override string Description => "Отменить последний заказанный трек";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override CommandVisibility Visibility => CommandVisibility.All;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        var hasUserId =
            parameters.TryGetValue("userId", out var userIdObj)
            && !string.IsNullOrWhiteSpace(userIdObj?.ToString());

        if (hasUserId)
        {
            var userId = userIdObj!.ToString()!;
            result = await commandsService.CancelLastTrackAsync(userId, cancellationToken);
        }
        else
        {
            result = "Не удалось определить пользователя";
        }

        return result;
    }
}

