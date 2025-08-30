using MARS.Server.Services.Twitch.Rewards.TwitchGaoAlert.Entitys;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchGaoAlert;

public class TwitchGaoAlert(
    ITwitchAPI api,
    ITwitchClient client,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    ILogger<TwitchGaoAlert> logger
) : BackgroundService
{
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
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;

        var cost = twEvent.Reward.Cost;
        var text = args.Notification.Payload.Event.UserInput;
        var channel = twEvent.BroadcasterUserId;

        if (
            channel.Equals(TwitchExstension.ChannelId, StringComparison.OrdinalIgnoreCase)
            && cost == 18
        )
        {
            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    text = text.Trim();
                    var isJustText = text.Contains(' ');

                    GaoAlertDto? gaoAlert;
                    if (!isJustText)
                    {
                        text = text.StartsWith('@') ? text.Substring(1) : text;
                        var twitchUser = await api.Helix.Users.GetUsersAsync(null, [text]);
                        if (twitchUser is { Users.Length: > 0 })
                        {
                            var user = twitchUser.Users.First();
                            gaoAlert = new GaoAlertDto() { TwitchUser = user, IsJustText = false };
                            await hubContext.Clients.All.GaoAlert(gaoAlert);
                            logger.LogInformation(
                                "Gao alert with user {userName}",
                                user.DisplayName
                            );
                            return;
                        }
                    }

                    gaoAlert = new GaoAlertDto() { IsJustText = true, JustText = text };
                    await hubContext.Clients.All.GaoAlert(gaoAlert);
                    logger.LogInformation("Gao alert with user {text}", text);
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    await client.SendMessageToMainTwitchAsync(
                        "Ошибка в ходе распознования GAO/NOT GAO"
                    );
                }
            });
        }
    }
}
