using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service;

namespace MARS.Server.Services.Twitch.Media;

public class TwitchMediaTranscodeWorker(
    IServiceScopeFactory serviceScopeFactory,
    ITwitchMediaPreparationService twitchMediaPreparationService,
    ILogger<TwitchMediaTranscodeWorker> logger
) : BackgroundService
{
    public bool IsServiceActive { get; set; } = true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TranscodePendingMediaAsync(stoppingToken);

        using var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        while (IsServiceActive && await periodicTimer.WaitForNextTickAsync(stoppingToken))
        {
            await TranscodePendingMediaAsync(stoppingToken);
        }
    }

    private async Task TranscodePendingMediaAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var randomMemeService = scope.ServiceProvider.GetRequiredService<IRandomMemeService>();
            var mediaOrders = await randomMemeService.GetAllMemeOrdersAsync(cancellationToken);

            foreach (var mediaOrder in mediaOrders)
            {
                await twitchMediaPreparationService.PrepareMediaAsync(
                    mediaOrder,
                    null,
                    cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось выполнить фоновую перекодировку медиа");
        }
    }
}