using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;

public class Confetty : BackgroundService, ITwitchReward
{
    public bool IsServiceActive { get; set; } = true;
    public int Cost { get; init; } = 1700;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hub;
    private readonly ITwitchClient _client;
    private readonly EventSubWebsocketClient _wsClient;

    public Confetty(
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
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;
        return
            twEvent.Reward.Cost == Cost
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            ? _hub.Clients.All.MakeScreenParticles(Entitys.TwitchScreenParticles.Confetty)
            : Task.CompletedTask;
    }
}
