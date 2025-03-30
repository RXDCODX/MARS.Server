using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.Twitch.AutoInfoFetch;

public class AutoRewardInfoFetcher(
    ITwitchAPI api,
    IDbContextFactory<AppDbContext> factory,
    TokenService tokenService
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Factory.StartNew(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                    await using var dbcontext = await factory.CreateDbContextAsync(stoppingToken);

                    var emptyAlerts = dbcontext
                        .Alerts.AsNoTracking()
                        .AsEnumerable()
                        .Where(e =>
                        {
                            var guid = e.MetaInfo.TwitchGuid;

                            return !guid.HasValue
                                || guid == Guid.Empty
                                || e.MetaInfo.TwitchPointsCost <= 0;
                        })
                        .ToList();
                    var twitchAlerts = await api.Helix.ChannelPoints.GetCustomRewardAsync(
                        TwitchExstension.ChannelId,
                        null,
                        false,
                        tokenService.Token?.AccessToken
                    );

                    foreach (var info in emptyAlerts)
                    {
                        var firstAlert = twitchAlerts.Data.FirstOrDefault(e =>
                            e.Cost == info.MetaInfo.TwitchPointsCost
                        );

                        if (firstAlert != default)
                        {
                            info.MetaInfo.TwitchGuid = Guid.Parse(firstAlert.Id);
                            dbcontext.Alerts.Update(info);
                        }
                    }

                    await dbcontext.SaveChangesAsync(stoppingToken);

                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            },
            TaskCreationOptions.LongRunning
        );
    }
}
