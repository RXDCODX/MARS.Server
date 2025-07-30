using MARS.Server.Services.ServiceManager;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;

public class Fireworks : ManagedServiceBase
{
    public override string ServiceName => "fireworks";
    public override string DisplayName => "Fireworks";
    public override string Description => "Фейерверки на Twitch";
    public override bool IsServiceActive { get; set; }
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
        : base(logger)
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

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Здесь можно добавить инициализацию, если потребуется
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        // Здесь можно добавить очистку ресурсов, если потребуется
        return base.StopAsync(cancellationToken);
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
