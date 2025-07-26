using System.Diagnostics;
using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Twitch.Management;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.CloseGameReward;

public class TwitchCloseTekkenService(
    IHostApplicationLifetime lifetime,
    ILogger<TwitchCloseTekkenService> logger,
    EventSubWebsocketClient wsClient
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "twitchclosetekken";
    public override string DisplayName => "Twitch Close Tekken";
    public override string Description => "Закрытие Tekken через Twitch";
    public override bool IsServiceActive { get; set; }

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;
        return base.StopAsync(cancellationToken);
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        var cost = twEvent.Reward.Cost;
        if (cost == 6666 && IsServiceActive)
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
