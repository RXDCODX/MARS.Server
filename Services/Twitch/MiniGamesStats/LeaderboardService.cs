using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.MiniGamesStats.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Twitch.MiniGamesStats;

public class LeaderboardService(
    IDbContextFactory<AppDbContext> factory,
    ITwitchUserEnsureService twitchUserEnsureService,
    ILogger<LeaderboardService> logger
) : ILeaderboardService
{
    public async Task RecordRouletteWinAsync(
        string twitchId,
        bool withWaifu,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

            var existing = await dbContext.TwitchLeaderboardUsers.FirstOrDefaultAsync(
                e => e.TwitchId == twitchId,
                cancellationToken
            );

            if (existing is not null)
            {
                await dbContext
                    .TwitchLeaderboardUsers.Where(e => e.TwitchId == twitchId)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters
                                .SetProperty(
                                    e => e.RussianRouletteWins,
                                    e => e.RussianRouletteWins + 1
                                )
                                .SetProperty(
                                    e => e.RussianRouletteWinsWithWaifu,
                                    e => e.RussianRouletteWinsWithWaifu + (withWaifu ? 1 : 0)
                                ),
                        cancellationToken
                    );
            }
            else
            {
                await twitchUserEnsureService.EnsureUserExistsAsync(twitchId, cancellationToken);

                dbContext.TwitchLeaderboardUsers.Add(
                    new TwitchLeaderboardUser
                    {
                        TwitchId = twitchId,
                        RussianRouletteWins = 1,
                        RussianRouletteWinsWithWaifu = withWaifu ? 1 : 0,
                    }
                );
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при записи победы в русскую рулетку для пользователя {TwitchId}",
                twitchId
            );
        }
    }

    public async Task RecordTriviaWinAsync(
        string twitchId,
        bool withWaifu,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

            var existing = await dbContext.TwitchLeaderboardUsers.FirstOrDefaultAsync(
                e => e.TwitchId == twitchId,
                cancellationToken
            );

            if (existing is not null)
            {
                await dbContext
                    .TwitchLeaderboardUsers.Where(e => e.TwitchId == twitchId)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters
                                .SetProperty(e => e.TriviaWins, e => e.TriviaWins + 1)
                                .SetProperty(
                                    e => e.TriviaWinsWithWaifus,
                                    e => e.TriviaWinsWithWaifus + (withWaifu ? 1 : 0)
                                ),
                        cancellationToken
                    );
            }
            else
            {
                await twitchUserEnsureService.EnsureUserExistsAsync(twitchId, cancellationToken);

                dbContext.TwitchLeaderboardUsers.Add(
                    new TwitchLeaderboardUser
                    {
                        TwitchId = twitchId,
                        TriviaWins = 1,
                        TriviaWinsWithWaifus = withWaifu ? 1 : 0,
                    }
                );
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при записи победы в викторину для пользователя {TwitchId}",
                twitchId
            );
        }
    }

    public async Task<IReadOnlyList<TwitchLeaderboardUser>> GetTopAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<TwitchLeaderboardUser> result = [];

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

            result = await dbContext
                .TwitchLeaderboardUsers.AsNoTracking()
                .Include(e => e.TwitchUser)
                .OrderByDescending(e => e.RussianRouletteWins + e.TriviaWins)
                .ThenByDescending(e => e.RussianRouletteWinsWithWaifu + e.TriviaWinsWithWaifus)
                .Take(count)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении топа победителей мини-игр");
        }

        return result;
    }

    public async Task<(int Place, TwitchLeaderboardUser? User)> GetUserStatsAsync(
        string twitchId,
        CancellationToken cancellationToken = default
    )
    {
        (int Place, TwitchLeaderboardUser? User) result = (0, null);

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

            var user = await dbContext
                .TwitchLeaderboardUsers.AsNoTracking()
                .Include(e => e.TwitchUser)
                .FirstOrDefaultAsync(e => e.TwitchId == twitchId, cancellationToken);

            if (user is not null)
            {
                var place =
                    await dbContext
                        .TwitchLeaderboardUsers.AsNoTracking()
                        .CountAsync(
                            e =>
                                e.RussianRouletteWins + e.TriviaWins
                                > user.RussianRouletteWins + user.TriviaWins,
                            cancellationToken
                        ) + 1;

                result = (place, user);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении статистики мини-игр для пользователя {TwitchId}",
                twitchId
            );
        }

        return result;
    }
}
