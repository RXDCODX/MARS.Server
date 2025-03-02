using MARS.Server.Services.Twitch.FumoFriday;
using MARS.Server.Services.Twitch.Rewards.TwitchAlerts;
using MARS.Server.Services.Twitch.Rewards.TwitchRandomMeme;
using MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;
using MARS.Server.Services.Twitch.StreamBotNotifications;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Management;

public class EventSubService(
    ITwitchAPI api,
    ILogger<EventSubService> logger,
    TwitchStreamStartupNotifications twitchStreamStartupNotifications,
    TwitchMediaAlerts twitchMediaAlerts,
    RollWaifu rollWaifu,
    RandomMeme twitchRandomMeme,
    ITelegramBotClient client,
    FumoFridayWorker fumoFridayWorker
)
{
    public readonly EventSubWebsocketClient WsClient = new();

    private bool _firstActivation = true;

    public async Task UpdatePubSubAsync(string? token = null)
    {
        if (!_firstActivation)
        {
            if (token != null)
            {
                var result = await GetEventSubsAsync(token);

                if (
                    result != null
                    && !result.Subscriptions.Any(e =>
                        e.Status.Equals("enabled", StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    await WsClient.DisconnectAsync();
                    await WsClient.ConnectAsync();
                }
            }
        }

        if (_firstActivation)
        {
            WsClient.StreamOnline += twitchStreamStartupNotifications.PubSubOnlineOnStreamUp;
            WsClient.StreamOffline += twitchStreamStartupNotifications.PubSibOfflineStream;
            WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                twitchMediaAlerts.TwitchClientOnOnMessageSend;
            WsClient.ChannelPointsCustomRewardRedemptionAdd += rollWaifu.RollWaifuTwitchEvent;
            WsClient.ChannelPointsCustomRewardRedemptionAdd += twitchRandomMeme.RandomMemeHandler;
            WsClient.ChannelPointsCustomRewardRedemptionAdd += fumoFridayWorker.OnRewardRedemption;

            WsClient.WebsocketConnected += (_, _) => ReconnectAsync(token);

            WsClient.ErrorOccurred += (_, args) =>
            {
                logger.LogException(args.Exception);
                return Task.CompletedTask;
            };

            WsClient.WebsocketReconnected += async (sender, args) =>
            {
                await ReconnectAsync(token);
                await Task.Delay(1000);
            };

            WsClient.WebsocketDisconnected += async (sender, args) =>
            {
                while (!await WsClient.ReconnectAsync())
                {
                    await Task.Delay(30 * 1000);
                }
            };

            _firstActivation = false;

            await WsClient.ConnectAsync();
        }
    }

    public async Task ReconnectAsync(string? token)
    {
        var condition = new Dictionary<string, string>
        {
            { "from_broadcaster_user_id", TwitchExstension.ChannelId },
        };

        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "channel.raid",
            "1",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );

        condition.Clear();
        condition.Add("broadcaster_user_id", TwitchExstension.ChannelId);

        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "stream.online",
            "1",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );

        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "stream.offline",
            "1",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );

        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "channel.channel_points_custom_reward_redemption.add",
            "1",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );

        condition.Add("moderator_user_id", TwitchExstension.ChannelId);

        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "channel.follow",
            "2",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );

        condition.Clear();

        var response = await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
            clientId: api.Settings.ClientId,
            accessToken: token
        );

        if (response.Subscriptions.Length < 1)
        {
            logger.LogError("Не получилось подписать EventSub");
        }
        else
        {
            var aa = response.Subscriptions.Select(e => e.Type).Distinct();
            var message = string.Join(Environment.NewLine, aa);
            await client.SendMessage(
                402763435,
                "Подключенные ивенты для твича: " + Environment.NewLine + message
            );
        }
    }

    public async Task<GetEventSubSubscriptionsResponse?> GetEventSubsAsync(string acctoken)
    {
        try
        {
            return await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
                clientId: api.Settings.ClientId,
                accessToken: acctoken
            );
        }
        catch (Exception e)
        {
            logger.LogException(e);
            return null;
        }
    }
}
