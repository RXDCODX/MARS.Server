using MARS.Server.Services.Twitch.MiniGamesStats.Entitys;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.MiniGamesStats;

public class TekkenVictorinaLeaderbord(
    IDbContextFactory<AppDbContext> factory,
    IHostApplicationLifetime lifetime,
    ITwitchClient client
) : BackgroundService
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    public async Task<TwitchLeaderboardUser[]> GetTopThree()
    {
        await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);
        var topThree = await dbContext
            .TwitchLeaderboardUsers.OrderByDescending(e => e.TekkenVictorinaWins)
            .AsNoTracking()
            .Take(3)
            .ToArrayAsync(cancellationToken: _cancellationToken);

        return topThree;
    }

    public async Task<(int order, TwitchLeaderboardUser user)?> GetUserStat(string twitchId)
    {
        await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);
        var userList = dbContext
            .TwitchLeaderboardUsers.OrderByDescending(e => e.TekkenVictorinaWins)
            .AsNoTracking()
            .AsAsyncEnumerable();

        var count = 0;

        await foreach (var twitchLeaderboardUser in userList)
        {
            ++count;

            if (twitchLeaderboardUser.TwitchId.Equals(twitchId, StringComparison.OrdinalIgnoreCase))
            {
                return (count, twitchLeaderboardUser);
            }
        }

        return null;
    }

    public async Task AddOrUpdateUserLeaderBoard(
        string twitchId,
        string name,
        bool isWaifuWin = false
    )
    {
        await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);
        var isExists = await dbContext.TwitchLeaderboardUsers.AnyAsync(
            e => e.TwitchId == twitchId,
            cancellationToken: _cancellationToken
        );

        if (isExists)
        {
            if (isWaifuWin)
            {
                await dbContext
                    .TwitchLeaderboardUsers.Where(e => e.TwitchId == twitchId)
                    .ExecuteUpdateAsync(
                        e =>
                            e.SetProperty(
                                    property => property.TekkenVictorinaWins,
                                    value => value.TekkenVictorinaWins + 1
                                )
                                .SetProperty(
                                    property => property.TekkenVictorinaWinsWithWaifu,
                                    value => value.TekkenVictorinaWinsWithWaifu + 1
                                ),
                        _cancellationToken
                    );
            }
            else
            {
                await dbContext
                    .TwitchLeaderboardUsers.Where(e => e.TwitchId == twitchId)
                    .ExecuteUpdateAsync(
                        e =>
                            e.SetProperty(
                                property => property.TekkenVictorinaWins,
                                value => value.TekkenVictorinaWins + 1
                            ),
                        cancellationToken: _cancellationToken
                    );
            }
        }
        else
        {
            var newUser = new TwitchLeaderboardUser()
            {
                TwitchId = twitchId,
                TekkenVictorinaWins = 1,
                TekkenVictorinaWinsWithWaifu = isWaifuWin ? 1 : 0,
            };

            await dbContext.TwitchLeaderboardUsers.AddAsync(newUser, _cancellationToken);
        }

        await dbContext.SaveChangesAsync(_cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += ClientOnOnMessageReceived;
        });
        return Task.CompletedTask;
    }

    private async void ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var message = e.ChatMessage.Message;
        var twitchId = e.ChatMessage.UserId!;
        var channel = e.ChatMessage.Channel;
        var user = e.ChatMessage.DisplayName;

        if (
            channel.Equals(TwitchExstension.Channel)
            && !TwitchExstension.BlackList.Any(t =>
                t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    switch (message)
                    {
                        case "!tekken_leaders":
                            var leaders = await GetTopThree();
                            var text = string.Join(
                                " | ",
                                leaders.Select(leaderboardUser =>
                                    $"[{leaderboardUser.TwitchUser?.DisplayName}]({leaderboardUser.TekkenVictorinaWins} /w WaifuHelp:{leaderboardUser.TekkenVictorinaWinsWithWaifu})"
                                )
                            );
                            await client.SendMessageToMainTwitchAsync($"@{user}, " + text);
                            break;
                        case "!tekken_me":
                            var userStats = await GetUserStat(twitchId);
                            if (userStats.HasValue)
                            {
                                await client.SendMessageToMainTwitchAsync(
                                    $"@{user}, твое место {userStats.Value.order} с ({userStats.Value.user.TekkenVictorinaWins} /w WaifuHelp: {userStats.Value.user.TekkenVictorinaWinsWithWaifu}) побед!"
                                );
                            }
                            else
                            {
                                await client.SendMessageToMainTwitchAsync(
                                    $"@{user}, нету информации о твоих победах в теккен викторине."
                                );
                            }

                            break;
                    }
                },
                _cancellationToken
            );
        }
    }
}
