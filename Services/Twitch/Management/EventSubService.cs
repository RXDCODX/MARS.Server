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
    private bool _isWsConnected;

    public async Task UpdateEventSubbAsync(TokenInfo? token = null)
    {
        if (!_firstActivation)
        {
            if (token != null)
            {
                var result = await GetEventSubsAsync(token);

                // Проверяем, есть ли активные подписки
                var hasActiveSubscriptions =
                    result != null
                    && result.Subscriptions.Any(e =>
                        e.Status.Equals("enabled", StringComparison.OrdinalIgnoreCase)
                    );

                if (!hasActiveSubscriptions)
                {
                    // Только если нет активных подписок, делаем переподписку
                    await ResubscribeToEventSub();
                }
                else if (!_isWsConnected)
                {
                    // Если подписки есть, но WebSocket отключен - пробуем переподключить
                    await TryReconnect();
                }
                // Если подписки активны и WebSocket подключен - ничего не делаем
            }
        }

        if (_firstActivation)
        {
            wsClient.WebsocketConnected += async (_, _) =>
            {
                _isWsConnected = true;
                await ResubscribeToEventSub();
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
                    await ResubscribeToEventSub();
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

        try
        {
            _isWsConnected = false;
            await wsClient.DisconnectAsync();
            await wsClient.ConnectAsync();
            _isWsConnected = true;

            if (!_isWsConnected)
            {
                logger.LogWarning("Не удалось переподключить EventSub WebSocket");
            }
        }
        catch
        {
            WsReconnectSlim.Release();
            await TryReconnect();
        }
        finally
        {
            WsReconnectSlim.Release();
        }
    }

    private async Task DeleteAllSubs(TokenInfo token)
    {
        var response = await GetEventSubsAsync(token);

        if (response != null)
        {
            foreach (var subscription in response.Subscriptions)
            {
                try
                {
                    await api.Helix.EventSub.DeleteEventSubSubscriptionAsync(
                        subscription.Id,
                        api.Settings.ClientId,
                        token.AccessToken
                    );
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    if (
                        await HandleUnauthorizedError(token, $"удаление подписки {subscription.Id}")
                    )
                    {
                        token = tokenService.Token ?? token;
                        await api.Helix.EventSub.DeleteEventSubSubscriptionAsync(
                            subscription.Id,
                            api.Settings.ClientId,
                            token.AccessToken
                        );
                    }
                    else
                    {
                        logger.LogError(
                            "Не удалось обновить токен для удаления подписки {SubscriptionId}",
                            subscription.Id
                        );
                    }
                }
            }
        }
    }

    private async Task<bool> EnsureWebSocketConnected()
    {
        if (_isWsConnected)
        {
            return true;
        }

        logger.LogWarning("WebSocket не подключен, пытаемся переподключить...");

        if (WsReconnectSlim.CurrentCount > 0)
        {
            await Task.Factory.StartNew(TryReconnect, _cancellationToken);
        }

        // Ждем короткое время, чтобы дать шанс на подключение
        await Task.Delay(5 * 1000, _cancellationToken);

        if (!_isWsConnected)
        {
            logger.LogError("Не удалось подключить WebSocket для создания подписок");
            return false;
        }

        return true;
    }

    private async Task<bool> HandleUnauthorizedError(TokenInfo token, string operation)
    {
        logger.LogWarning("Получена ошибка 401 при {Operation}. Обновляем токен...", operation);
        var refreshResult = await tokenService.RefreshTokenAsync(token);
        if (refreshResult)
        {
            logger.LogInformation("Токен успешно обновлен для {Operation}", operation);
            return true;
        }
        else
        {
            logger.LogError("Не удалось обновить токен для {Operation}", operation);
            return false;
        }
    }

    public async Task<string> ResubscribeToEventSub(TokenInfo? token = default)
    {
        if (SemaphoreSlim.CurrentCount == 0)
        {
            return "Семафор запретил заход";
        }

        await SemaphoreSlim.WaitAsync(_cancellationToken);

        if (token == null)
        {
            await tokenService.EnsureActualTokenAsync(_cancellationToken);
            token = tokenService.Token;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(token?.AccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(token?.RefreshToken);

        try
        {
            await DeleteAllSubs(token);

            // Проверяем подключение WebSocket перед созданием подписок
            if (!await EnsureWebSocketConnected())
            {
                return "Ошибка: не удалось подключить WebSocket для создания подписок";
            }

            var condition = new Dictionary<string, string>
            {
                { "to_broadcaster_user_id", TwitchExstension.ChannelId },
            };

            // Проверяем подключение перед каждой подпиской
            if (!await EnsureWebSocketConnected())
            {
                return "Ошибка: WebSocket отключился при создании подписки на channel.raid";
            }

            try
            {
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
            }
            catch (HttpRequestException httpEx)
                when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
            {
                if (await HandleUnauthorizedError(token, "создание подписки на channel.raid"))
                {
                    token = tokenService.Token ?? token;
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
                }
                else
                {
                    return "Ошибка: не удалось обновить токен для создания подписки на channel.raid";
                }
            }

            condition.Clear();
            condition.Add("broadcaster_user_id", TwitchExstension.ChannelId);

            if (!await EnsureWebSocketConnected())
            {
                return "Ошибка: WebSocket отключился при создании подписки на stream.online";
            }

            try
            {
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
            }
            catch (HttpRequestException httpEx)
                when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
            {
                if (await HandleUnauthorizedError(token, "создание подписки на stream.online"))
                {
                    token = tokenService.Token ?? token;
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
                }
                else
                {
                    return "Ошибка: не удалось обновить токен для создания подписки на stream.online";
                }
            }

            if (!await EnsureWebSocketConnected())
            {
                return "Ошибка: WebSocket отключился при создании подписки на stream.offline";
            }

            try
            {
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
            }
            catch (HttpRequestException httpEx)
                when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
            {
                if (await HandleUnauthorizedError(token, "создание подписки на stream.offline"))
                {
                    token = tokenService.Token ?? token;
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
                }
                else
                {
                    return "Ошибка: не удалось обновить токен для создания подписки на stream.offline";
                }
            }

            if (!await EnsureWebSocketConnected())
            {
                return "Ошибка: WebSocket отключился при создании подписки на channel.channel_points_custom_reward_redemption.add";
            }

            try
            {
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
            }
            catch (HttpRequestException httpEx)
                when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
            {
                if (
                    await HandleUnauthorizedError(
                        token,
                        "создание подписки на channel.channel_points_custom_reward_redemption.add"
                    )
                )
                {
                    token = tokenService.Token ?? token;
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
                }
                else
                {
                    return "Ошибка: не удалось обновить токен для создания подписки на channel.channel_points_custom_reward_redemption.add";
                }
            }

            if (!await EnsureWebSocketConnected())
            {
                return "Ошибка: WebSocket отключился при создании подписки на channel.moderator.add";
            }

            try
            {
                await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.moderator.add",
                    "1",
                    condition,
                    EventSubTransportMethod.Websocket,
                    wsClient.SessionId,
                    null,
                    null,
                    api.Settings.ClientId,
                    token.AccessToken
                );
            }
            catch (HttpRequestException httpEx)
                when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
            {
                if (
                    await HandleUnauthorizedError(
                        token,
                        "создание подписки на channel.moderator.add"
                    )
                )
                {
                    token = tokenService.Token ?? token;
                    await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                        "channel.moderator.add",
                        "1",
                        condition,
                        EventSubTransportMethod.Websocket,
                        wsClient.SessionId,
                        null,
                        null,
                        api.Settings.ClientId,
                        token.AccessToken
                    );
                }
                else
                {
                    return "Ошибка: не удалось обновить токен для создания подписки на channel.moderator.add";
                }
            }

            if (!await EnsureWebSocketConnected())
            {
                return "Ошибка: WebSocket отключился при создании подписки на channel.vip.add";
            }

            try
            {
                await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.vip.add",
                    "1",
                    condition,
                    EventSubTransportMethod.Websocket,
                    wsClient.SessionId,
                    null,
                    null,
                    api.Settings.ClientId,
                    token.AccessToken
                );
            }
            catch (HttpRequestException httpEx)
                when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
            {
                if (await HandleUnauthorizedError(token, "создание подписки на channel.vip.add"))
                {
                    token = tokenService.Token ?? token;
                    await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                        "channel.vip.add",
                        "1",
                        condition,
                        EventSubTransportMethod.Websocket,
                        wsClient.SessionId,
                        null,
                        null,
                        api.Settings.ClientId,
                        token.AccessToken
                    );
                }
                else
                {
                    return "Ошибка: не удалось обновить токен для создания подписки на channel.vip.add";
                }
            }

            condition.Add("moderator_user_id", TwitchExstension.ChannelId);

            if (!await EnsureWebSocketConnected())
            {
                return "Ошибка: WebSocket отключился при создании подписки на channel.follow";
            }

            try
            {
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
            }
            catch (HttpRequestException httpEx)
                when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
            {
                if (await HandleUnauthorizedError(token, "создание подписки на channel.follow"))
                {
                    token = tokenService.Token ?? token;
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
                }
                else
                {
                    return "Ошибка: не удалось обновить токен для создания подписки на channel.follow";
                }
            }

            condition.Clear();

            GetEventSubSubscriptionsResponse? response;
            try
            {
                response = await api
                    .Helix.EventSub.GetEventSubSubscriptionsAsync(
                        clientId: api.Settings.ClientId,
                        accessToken: token.AccessToken
                    )
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException httpEx)
                when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
            {
                if (await HandleUnauthorizedError(token, "получение списка подписок EventSub"))
                {
                    token = tokenService.Token ?? token;
                    response = await api
                        .Helix.EventSub.GetEventSubSubscriptionsAsync(
                            clientId: api.Settings.ClientId,
                            accessToken: token.AccessToken
                        )
                        .ConfigureAwait(false);
                }
                else
                {
                    return "Ошибка: не удалось обновить токен для получения списка подписок EventSub";
                }
            }

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
        catch (HttpRequestException httpEx)
            when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
        {
            logger.LogWarning(
                "Получена ошибка 401 (Unauthorized) при получении EventSub подписок. Пытаемся обновить токен..."
            );

            // Пытаемся обновить токен
            var refreshResult = await tokenService.RefreshTokenAsync(token);
            if (refreshResult)
            {
                logger.LogInformation("Токен успешно обновлен, повторяем запрос...");
                // Повторяем запрос с обновленным токеном
                return await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
                    clientId: api.Settings.ClientId,
                    accessToken: tokenService.Token?.AccessToken ?? token.AccessToken
                );
            }
            else
            {
                logger.LogError("Не удалось обновить токен после получения ошибки 401");
                return null;
            }
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
