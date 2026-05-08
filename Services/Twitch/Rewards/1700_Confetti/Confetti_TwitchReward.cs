using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._1700_Confetti;

public class Confetti_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Confetti_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    RickRollerService rickRollerService
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎉 Конфетти!";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(0, 255, 47);
    public override int Cost { get; init; } = 1700;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);

        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        });
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
        var twEvent = args.Payload.Event;
        if (
            twEvent.Reward.Cost == Cost
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            await rickRollerService.TryRickRollAsync(
                TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                () => hubContext.Clients.All.MakeScreenParticles(TwitchScreenParticles.Confetty)
            );
        }
    }
}
