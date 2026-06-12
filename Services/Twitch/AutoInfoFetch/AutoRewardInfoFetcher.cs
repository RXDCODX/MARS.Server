using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Interfaces;

namespace MARS.Server.Services.Twitch.AutoInfoFetch;

public class AutoRewardInfoFetcher(
    ITwitchAPI api,
    IDbContextFactory<AppDbContext> factory,
    TokenService tokenService,
    ILogger<AutoRewardInfoFetcher> logger
) : BackgroundService
{
    private Timer? _timer;
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        if (!stoppingToken.IsCancellationRequested)
        {
            await FetchRewardInfoAsync();

            // Настройка таймера на 10 минут
            _timer = new Timer(TimeSpan.FromMinutes(10));
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            if (!_stoppingToken.IsCancellationRequested)
            {
                await FetchRewardInfoAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }

    private async Task FetchRewardInfoAsync()
    {
        try
        {
            await using var dbcontext = await factory.CreateDbContextAsync(_stoppingToken);

            var twitchAlerts = await api.Helix.ChannelPoints.GetCustomRewardAsync(
                TwitchExstension.ChannelId,
                null,
                false,
                tokenService.Token?.AccessToken
            );

            await foreach (
                var info in dbcontext.Alerts.AsAsyncEnumerable().WithCancellation(_stoppingToken)
            )
            {
                var firstAlert = twitchAlerts.Data.FirstOrDefault(e =>
                    e.Cost == info.MetaInfo.TwitchPointsCost
                );

                if (firstAlert != null)
                {
                    info.MetaInfo.TwitchGuid = Guid.Parse(firstAlert.Id);
                    dbcontext.Alerts.Update(info);
                }
            }

            await dbcontext.SaveChangesAsync(_stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }

    public override void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
