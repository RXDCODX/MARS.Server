using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class QueuePositionCommand(CommandsService commandsService) : BaseCommand
{
    public override string CommandName => "queue";
    public override string Description => "Показать вашу позицию в очереди звуковых запросов";
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
            result = await commandsService.GetUserQueuePositionAsync(userId);
        }
        else
        {
            result = "Не удалось определить пользователя";
        }

        return result;
    }
}

