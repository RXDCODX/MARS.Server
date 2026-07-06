using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._6666_CloseGame;

public class CloseGame_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<CloseGame_TwitchReward> logger,
    IHostEnvironment environment,
    EventSubWebsocketClient wsClient,
    ITwitchClient client,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "💻 Выключить игру";
    public override string AlertDescription { get; set; } = "❌ Закрывает Tekken 8 или Dota 2";
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
        var vr = await validator
            .ForRedemption(args)
            .RequireBroadcasterUserId()
            .RequireRewardEnabled(IsRewardEnabled)
            .RequireCost(Cost)
            .RequireFollower()
            .ValidateWithResponseAsync(args.Payload.Event.UserName);

        if (vr.IsInvalid)
        {
            return;
        }

        await Task.Factory.StartNew(() =>
        {
            var processNames = new[] { "Polaris-Win64-Shipping", "dota2" };
            foreach (var name in processNames)
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var process in processes)
                {
                    try
                    {
                        process.CloseMainWindow();
                        process.Kill();
                        process.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        logger.LogException(ex);
                    }
                }
            }
        });
    }
}
