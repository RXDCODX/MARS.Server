using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;

public class Fireworks : BackgroundService
{
    public bool IsServiceActive { get; set; }
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hub;
    private readonly ITwitchClient _client;
    private readonly EventSubWebsocketClient _wsClient;

    public Fireworks(
        ILogger<Fireworks> logger,
        IHubContext<TelegramusHub, ITelegramusHub> hub,
        IHostApplicationLifetime lifetime,
        ITwitchClient client,
        EventSubWebsocketClient wsClient
    )
        : base()
    {
        _hub = hub;
        _client = client;
        _wsClient = wsClient;
        lifetime.ApplicationStarted.Register(() =>
        {
            _wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
    }

    private Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        return
            twEvent.Reward.Cost == 1701
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            ? _hub.Clients.All.MakeScreenParticles(Entitys.TwitchScreenParticles.Fireworks)
            : Task.CompletedTask;
    }
}
