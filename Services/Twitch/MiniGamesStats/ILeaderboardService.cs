using MARS.Server.Services.Twitch.MiniGamesStats.Entitys;

namespace MARS.Server.Services.Twitch.MiniGamesStats;

public interface ILeaderboardService
{
    Task RecordRouletteWinAsync(
        string twitchId,
        bool withWaifu,
        CancellationToken cancellationToken = default
    );

    Task RecordTriviaWinAsync(
        string twitchId,
        bool withWaifu,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<TwitchLeaderboardUser>> GetTopAsync(
        int count,
        CancellationToken cancellationToken = default
    );

    Task<(int Place, TwitchLeaderboardUser? User)> GetUserStatsAsync(
        string twitchId,
        CancellationToken cancellationToken = default
    );
}
