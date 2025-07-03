using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;

public class Confetty : BackgroundService
{
    private readonly ILogger<Confetty> _logger;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hub;
    private readonly ITwitchClient _client;

    public Confetty(
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
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });
    }

    private Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        return
            twEvent.Reward.Cost == 1700
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            ? _hub.Clients.All.MakeScreenParticles(Entitys.TwitchScreenParticles.Confetty)
            : Task.CompletedTask;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
