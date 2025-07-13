using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.AutoInfoFetch;

public class TwitchNameActualizer(
    ITwitchClient client,
    IDbContextFactory<AppDbContext> factory,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private static readonly List<string> CachedTwitchIds = [];
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

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
        var userId = e.ChatMessage.UserId;
        var userName = e.ChatMessage.DisplayName;

        if (CachedTwitchIds.Contains(userId))
        {
            return;
        }

        await Task.Factory.StartNew(
            async () =>
            {
                await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);
                var host = await dbContext.Hosts.FindAsync(userId);
                var twitchLeaderboardUser = await dbContext.TwitchLeaderboardUsers.FindAsync(
                    userId
                );
                var fumoUsers = await dbContext.FumoUsers.FindAsync(userId);
                var helloVideosUser = await dbContext.HelloVideosUsers.FirstOrDefaultAsync(
                    e => e.TwitchId == userId,
                    cancellationToken: _cancellationToken
                );

                CachedTwitchIds.Add(userId);

                if (host != null && host.Name != userName)
                {
                    host.Name = userName;
                }

                if (twitchLeaderboardUser != null && twitchLeaderboardUser.DisplayName != userName)
                {
                    twitchLeaderboardUser.DisplayName = userName;
                }

                if (fumoUsers != null && fumoUsers.DisplayName != userName)
                {
                    fumoUsers.DisplayName = userName;
                }

                if (helloVideosUser != null && helloVideosUser.Name != userName)
                {
                    helloVideosUser.Name = userName;
                }

                await dbContext.SaveChangesAsync(_cancellationToken);
            },
            _cancellationToken
        );
    }
}
