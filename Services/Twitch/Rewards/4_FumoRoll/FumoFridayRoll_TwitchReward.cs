using System;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._4_FumoRoll;

public class FumoFridayRoll_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<FumoFridayRoll_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    FumoRollService fumoRollService,
    ITwitchAPI api,
    ITwitchClient client,
    TwitchUserEnsureService ensureService
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🧸 Fumo Roulette";

    public override string AlertDescription { get; set; } =
        "💰 Цена - 4 балла. Крути рулетку Fumo по пятницам! ♪";

    public override Color Color { get; set; } = Color.FromArgb(255, 182, 193);

    public override int Cost { get; init; } = 4;

    //public override Func<bool> IsRewardEnabled { get; set; } =
    //    () => DateTime.Now.DayOfWeek == DayOfWeek.Friday;

    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);

        wsClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsCustomRewardRedemption;
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
        ChannelPointsCustomRewardRedemption? twEvent = args.Payload.Event;
        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            if (twEvent.Reward.Cost == Cost)
            {
                await Task.Factory.StartNew(async () =>
                {
                    var fumo = await fumoRollService.RollTheFumo();

                    if (fumo is not null)
                    {
                        var user = await ensureService.EnsureUserExistsAsync(twEvent.UserId);

                        await hubContext.Clients.All.FumoRoll(fumo, user);
                    }
                    else
                    {
                        await client.SendMessageToMainTwitchAsync(
                            $"@{twEvent.UserName}, не удалось найти Fumo :(",
                            logger
                        );
                    }
                });
            }
        }
    }
}
