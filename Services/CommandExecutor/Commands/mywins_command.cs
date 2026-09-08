using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.MiniGamesStats;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class MyWinsCommand(ILeaderboardService leaderboardService) : BaseCommand
{
    public override string CommandName => "mywins";
    public override string Description => "Показывает ваши победы в мини-играх";
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
            var (place, user) = await leaderboardService.GetUserStatsAsync(
                userId,
                cancellationToken
            );

            if (user is not null)
            {
                var name = user.TwitchUser?.DisplayName ?? userId;
                result =
                    $"@{name}, вы на {place} месте! Побед: {user.TotalWins} (рулетка: {user.RussianRouletteWins}, рулетка с вайфу: {user.RussianRouletteWinsWithWaifu}, викторина: {user.TriviaWins}, викторина с вайфу: {user.TriviaWinsWithWaifus}).";
            }
            else
            {
                result = "У вас пока нет побед в мини-играх.";
            }
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
