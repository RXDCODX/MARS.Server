using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchTiktokEdit;

public class TwitchPhonkEditService(
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext
) : BackgroundService, ITwitchReward
{
    public int Cost { get; init; } = 337;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(
            () =>
                wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                    WsClientOnChannelPointsCustomRewardRedemptionAdd
        );

        lifetime.ApplicationStopping.Register(
            () =>
                wsClient.ChannelPointsCustomRewardRedemptionAdd -=
                    WsClientOnChannelPointsCustomRewardRedemptionAdd
        );

        return Task.CompletedTask;
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;

        var cost = twEvent.Reward.Cost;
        var text = args.Payload.Event.UserInput;
        var channel = twEvent.BroadcasterUserId;

        if (
            channel.Equals(TwitchExstension.ChannelId, StringComparison.OrdinalIgnoreCase)
            && cost == Cost
        )
        {
            await Task.Factory.StartNew(async () =>
            {
                await hubContext.Clients.All.PhonkEdit();
            });
        }
    }
}
