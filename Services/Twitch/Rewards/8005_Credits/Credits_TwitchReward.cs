using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._8005_Credits;

public class Credits_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Credits_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎬 Credits";
    public override string AlertDescription { get; set; } = "🎞️ Показать кредиты на экране";
    public override Color Color { get; set; } = Color.FromArgb(255, 215, 0);
    public override int Cost { get; init; } = 8005;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    public bool IsServiceActive { get; set; } = true;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;
        await base.StopAsync(cancellationToken);
    }

    private async Task OnChannelPointsCustomRewardRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var vr = await validator
            .ForRedemption(args)
            .RequireBroadcasterUserId()
            .RequireServiceActive(IsServiceActive)
            .RequireCost(Cost)
            .ValidateAsync();

        if (vr.IsInvalid)
        {
            return;
        }

        var twEvent = args.Payload.Event;

        try
        {
            logger.LogInformation(
                "Credits награда активирована пользователем {UserName} за {Cost} баллов",
                twEvent.UserName,
                twEvent.Reward.Cost
            );

            await hubContext.Clients.All.Credits();

            logger.LogInformation(
                "Credits эффект активирован для пользователя {UserName}",
                twEvent.UserName
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }
}
