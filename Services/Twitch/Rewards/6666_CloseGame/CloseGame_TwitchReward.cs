using System.Diagnostics;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._6666_CloseGame;

public class CloseGame_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<CloseGame_TwitchReward> logger,
    IHostEnvironment environment,
    EventSubWebsocketClient wsClient
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Выключить теккен";
    public override string AlertDescription { get; set; } = "Закрывает игрульку";
    public override Color Color { get; set; } = Color.FromArgb(0, 128, 255);
    public override int Cost { get; init; } = 6666;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    public override Task StartAsync(CancellationToken stoppingToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd +=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;

        return base.StartAsync(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;
        return base.StopAsync(cancellationToken);
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;
        var cost = twEvent.Reward.Cost;
        if (cost == Cost && IsRewardEnabled())
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
