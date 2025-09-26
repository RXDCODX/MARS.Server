using System.Diagnostics;
using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.CloseGameReward;

public class TwitchCloseTekkenService(
    ILogger<TwitchCloseTekkenService> logger,
    EventSubWebsocketClient wsClient
) : BackgroundService, ITwitchReward
{
    public bool IsServiceActive { get; set; } = true;
    public int Cost { get; init; } = 6666;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd +=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;
        await base.StopAsync(cancellationToken);
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;
        var cost = twEvent.Reward.Cost;
        if (cost == Cost && IsServiceActive)
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
