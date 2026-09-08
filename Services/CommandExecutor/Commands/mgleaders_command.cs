using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.MiniGamesStats;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class MGLeadersCommand(ILeaderboardService leaderboardService) : BaseCommand
{
    private const int TopCount = 3;

    public override string CommandName => "mgleaders";
    public override string Description => "Показывает топ победителей мини-игр";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms =>
        [Platform.Twitch, Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Данные о победителях пока отсутствуют.";

        var top = await leaderboardService.GetTopAsync(TopCount, cancellationToken);

        if (top.Count > 0)
        {
            var parts = top.Select(
                (user, index) =>
                {
                    var name = user.TwitchUser?.DisplayName ?? user.TwitchId;

                    return $"{index + 1}. {name} — {user.TotalWins} побед (рулетка: {user.RussianRouletteWins}, викторина: {user.TriviaWins})";
                }
            );

            result = "Топ мини-игр: " + string.Join("; ", parts);
        }

        return result;
    }
}
