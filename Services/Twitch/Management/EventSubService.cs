using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Management;

public class EventSubService(
    ITwitchAPI api,
    ILogger<EventSubService> logger,
    ITelegramBotClient client,
    TokenService tokenService
)
{
    public static readonly EventSubWebsocketClient WsClient = new();

    private static readonly SemaphoreSlim SemaphoreSlim = new(1);
    private bool _firstActivation = true;

    public async Task UpdateEventSubbAsync(TokenInfo? token = null)
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
                }
            }
        }

        if (_firstActivation)
        {
            WsClient.WebsocketConnected += (_, _) => ReconnectAsync(token);

            WsClient.ErrorOccurred += async (_, args) =>
            {
                logger.LogException(args.Exception);

                while (!await WsClient.ReconnectAsync())
                {
                    await Task.Delay(30 * 1000);
                }
            };

            WsClient.WebsocketReconnected += async (sender, args) =>
            {
                if (token != null)
                {
                    await ReconnectAsync(token);
                }

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

            if (token != null)
            {
                var subs = await GetEventSubsAsync(token);
                if (subs?.Subscriptions is { Length: > 0 })
                {
                    await DeleteAllSubs(token);
                }
            }

            await WsClient.ConnectAsync();
        }
    }

    private async Task DeleteAllSubs(TokenInfo token)
    {
        var response = await GetEventSubsAsync(token);

        if (response != null)
        {
            foreach (var subscription in response.Subscriptions)
            {
                await api.Helix.EventSub.DeleteEventSubSubscriptionAsync(
                    subscription.Id,
                    api.Settings.ClientId,
                    token.AccessToken
                );
            }
        }
    }

    public async Task ReconnectAsync(TokenInfo? token = default)
    {
        token ??= tokenService.Token;
        ArgumentException.ThrowIfNullOrWhiteSpace(token?.AccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(token?.RefreshToken);

        if (SemaphoreSlim.CurrentCount == 0)
        {
            return;
        }

        await SemaphoreSlim.WaitAsync();
        await DeleteAllSubs(token);

        var condition = new Dictionary<string, string>
        {
            { "to_broadcaster_user_id", TwitchExstension.ChannelId },
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
            token.AccessToken
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
            token.AccessToken
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
            token.AccessToken
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
            token.AccessToken
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
            token.AccessToken
        );

        condition.Clear();

        var response = await api
            .Helix.EventSub.GetEventSubSubscriptionsAsync(
                clientId: api.Settings.ClientId,
                accessToken: token.AccessToken
            )
            .ConfigureAwait(false);

        SemaphoreSlim.Release(1);

        if (response.Subscriptions.Length < 1)
        {
            logger.LogError("Не получилось подписать EventSub");
        }
        else
        {
            var aa = response.Subscriptions.Select(e => e.Type).Distinct();
            var message = string.Join(Environment.NewLine, aa);
            await client.SendMessage(
                TelegramExstension.Rxdcodx,
                "Подключенные ивенты для твича: " + Environment.NewLine + message
            );
        }
    }

    public async Task<GetEventSubSubscriptionsResponse?> GetEventSubsAsync(TokenInfo token)
    {
        try
        {
            return await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
                clientId: api.Settings.ClientId,
                accessToken: token.AccessToken
            );
        }
        catch (Exception e)
        {
            logger.LogException(e);
            return null;
        }
    }
}
