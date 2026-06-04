using System.Timers;

namespace MARS.Server.Services.Twitch.AutoInfoFetch;

public class AutoRewardInfoFetcher(
    ITwitchAPI api,
    IDbContextFactory<AppDbContext> factory,
    TokenService tokenService
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

            // Настройка таймера на 30 минут
            _timer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }

        // Ожидание отмены
        await Task.CompletedTask;
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (!_stoppingToken.IsCancellationRequested)
        {
            await FetchRewardInfoAsync();
        }
    }

    private async Task FetchRewardInfoAsync()
    {
        try
        {
            await using var dbcontext = await factory.CreateDbContextAsync(_stoppingToken);

            var emptyAlerts = dbcontext
                .Alerts.AsNoTracking()
                .AsEnumerable()
                .Where(e =>
                {
                    var guid = e.MetaInfo.TwitchGuid;

                    return !guid.HasValue || guid == Guid.Empty || e.MetaInfo.TwitchPointsCost <= 0;
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

                if (firstAlert != null)
                {
                    info.MetaInfo.TwitchGuid = Guid.Parse(firstAlert.Id);
                    dbcontext.Alerts.Update(info);
                }
            }

            await dbcontext.SaveChangesAsync(_stoppingToken);
        }
        catch (Exception)
        {
            // Игнорируем ошибки при отмене или других проблемах
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
