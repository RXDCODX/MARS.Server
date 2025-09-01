using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Management;

public class EventSubService(
    ITwitchAPI api,
    ILogger<EventSubService> logger,
    ITelegramBotClient client,
    TokenService tokenService,
    IHostApplicationLifetime lifetime,
    EventSubWebsocketClient wsClient
) : BackgroundService
{
    private static readonly SemaphoreSlim SemaphoreSlim = new(1);
    private static readonly SemaphoreSlim WsReconnectSlim = new(1);
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;
    private bool _firstActivation = true;
    private bool _isWsConnected = false;

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
                    await wsClient.DisconnectAsync();
                }
            }

            await ResubscribeToEventSub(token);
        }

        if (_firstActivation)
        {
            wsClient.WebsocketConnected += async (_, _) =>
            {
                _isWsConnected = true;
                await ResubscribeToEventSub(token);
            };

            wsClient.ErrorOccurred += async (_, args) =>
            {
                logger.LogException(args.Exception);

                await TryReconnect();
            };

            wsClient.WebsocketReconnected += async (sender, args) =>
            {
                _isWsConnected = true;
                if (token != null)
                {
                    await ResubscribeToEventSub(token);
                }

                await Task.Delay(1000, _cancellationToken);
            };

            wsClient.WebsocketDisconnected += async (sender, args) =>
            {
                await TryReconnect();
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

            await wsClient.ConnectAsync();
        }
    }

    private async Task TryReconnect()
    {
        if (WsReconnectSlim.CurrentCount < 1)
        {
            return;
        }

        await WsReconnectSlim.WaitAsync(_cancellationToken);

        _isWsConnected = false;
        while (true)
        {
            _isWsConnected = await wsClient.ReconnectAsync();
            if (_isWsConnected)
            {
                break;
            }
            else
            {
                await Task.Delay(30 * 1000, _cancellationToken);
            }
        }

        WsReconnectSlim.Release();
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

    public async Task<string> ResubscribeToEventSub(TokenInfo? token = default, bool force = false)
    {
        token ??= tokenService.Token;
        ArgumentException.ThrowIfNullOrWhiteSpace(token?.AccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(token?.RefreshToken);

        try
        {
            await DeleteAllSubs(token);

            if (!_isWsConnected)
            {
                while (!_isWsConnected)
                {
                    if (WsReconnectSlim.CurrentCount > 0)
                    {
                        await Task.Factory.StartNew(TryReconnect, _cancellationToken);
                    }

                    await Task.Delay(30 * 1000, _cancellationToken);
                }
            }

            var condition = new Dictionary<string, string>
            {
                { "to_broadcaster_user_id", TwitchExstension.ChannelId },
            };

            await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.raid",
                "1",
                condition,
                EventSubTransportMethod.Websocket,
                wsClient.SessionId,
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
                wsClient.SessionId,
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
                wsClient.SessionId,
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
                wsClient.SessionId,
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
                wsClient.SessionId,
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
                return "Ошибка: не удалось подписаться на EventSub";
            }
            else
            {
                var aa = response.Subscriptions.Select(e => e.Type).Distinct();
                var message = string.Join(Environment.NewLine, aa);
                await client.SendMessage(
                    TelegramExstension.Rxdcodx,
                    "Подключенные ивенты для твича: " + Environment.NewLine + message,
                    cancellationToken: _cancellationToken
                );
                return $"Реконект EventSub выполнен успешно. Подписки: {string.Join(", ", aa)}";
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return $"Ошибка при реконекте EventSub: {ex.Message}";
        }
        finally
        {
            SemaphoreSlim.Release(1);
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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            Task.Factory.StartNew(
                async () =>
                {
                    await UpdateEventSubbAsync(tokenService.Token);
                },
                stoppingToken
            );
        });

        return Task.CompletedTask;
    }
}
