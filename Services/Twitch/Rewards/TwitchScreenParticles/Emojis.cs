using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;

public class Emojis
{
    private readonly ILogger<Confetty> _logger;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hub;
    private readonly ITwitchClient _client;

    public Emojis(
        EventSubService eventSubService,
        ILogger<Confetty> logger,
        IHubContext<TelegramusHub, ITelegramusHub> hub,
        IHostApplicationLifetime lifetime,
        ITwitchClient client
    )
    {
        _logger = logger;
        _hub = hub;
        _client = client;
        lifetime.ApplicationStarted.Register(() =>
        {
            eventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });
    }

    private Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        if (
            twEvent.Reward.Cost == 1702
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return _hub.Clients.All.MakeScreenEmojisParticles(twEvent.UserInput);
        }

        return Task.CompletedTask;
    }
}
