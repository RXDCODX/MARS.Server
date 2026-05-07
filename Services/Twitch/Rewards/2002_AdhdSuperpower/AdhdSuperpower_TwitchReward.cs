using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._2002_AdhdSuperpower;

public class AdhdSuperpower_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<AdhdSuperpower_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    RickRollerService rickRollerService
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "⚡ ADHD Superpower";
    public override string AlertDescription { get; set; } = "🔥 Активируй ADHD режим на 60 секунд!";
    public override Color Color { get; set; } = Color.FromArgb(138, 43, 226);
    public override int Cost { get; init; } = 2002;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    private const int AdhdDurationSeconds = 60;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsCustomRewardRedemption;
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;
        return base.StopAsync(cancellationToken);
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
            try
            {
                await rickRollerService.TryRickRollAsync(
                    TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                    async () =>
                    {
                        logger.LogInformation(
                            "ADHD награда активирована пользователем {UserName} за {Cost} поинтов",
                            twEvent.UserName,
                            twEvent.Reward.Cost
                        );

                        await hubContext.Clients.All.Adhd(AdhdDurationSeconds);

                        logger.LogInformation(
                            "ADHD эффект активирован на {Duration} секунд для пользователя {UserName}",
                            AdhdDurationSeconds,
                            twEvent.UserName
                        );
                    }
                );
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }
    }
}
