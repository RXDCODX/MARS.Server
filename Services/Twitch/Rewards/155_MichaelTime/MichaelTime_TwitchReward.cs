namespace MARS.Server.Services.Twitch.Rewards._155_MichaelTime;

public class MichaelTime_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<MichaelTime_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    RickRollerService rickRollerService
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "⏰ Michael Time!";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(255, 0, 0);
    public override int Cost { get; init; } = 155;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd -=
                OnChannelPointsCustomRewardRedemption;
        });

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
                            "MichaelJackson награда активирована пользователем {UserName} за {Cost} баллов",
                            twEvent.UserName,
                            twEvent.Reward.Cost
                        );

                        await hubContext.Clients.All.MichaelJackson();

                        logger.LogInformation(
                            "MichaelJackson эффект активирован для пользователя {UserName}",
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
