using System.Diagnostics;
using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.Twitch.Rewards.CloseGameReward;

public class TwitchCloseTekkenService(
    IHostApplicationLifetime lifetime,
    ILogger<TwitchCloseTekkenService> logger
) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });
        return Task.CompletedTask;
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        var cost = twEvent.Reward.Cost;
        if (cost == 6666)
        {
            await Task.Factory.StartNew(() =>
            {
                var processes = Process.GetProcessesByName("Polaris-Win64-Shipping");
                foreach (var process in processes)
                {
                    try
                    {
                        process.CloseMainWindow();
                        process.Kill();
                        process.WaitForExit(); // Ожидаем завершение процесса
                    }
                    catch (Exception ex)
                    {
                        logger.LogException(ex);
                    }
                }
            });
        }
    }
}
