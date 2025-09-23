using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;

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

    public async Task UpdateEventSubAsync()
    {
        if (!_firstActivation)
        {
            var token = tokenService.Token;
            if (token != null)
            {
                var result = await GetEventSubsAsync();

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
            }
        }

        if (_firstActivation)
        {
            wsClient.WebsocketConnected += WsClientOnWebsocketConnected;
            wsClient.ErrorOccurred += WsClientOnErrorOccurred;
            wsClient.WebsocketReconnected += WsClientOnWebsocketReconnected;
            wsClient.WebsocketDisconnected += WsClientOnWebsocketDisconnected;

            _firstActivation = false;

            if (tokenService.Token != null)
            {
                var subs = await GetEventSubsAsync();
                if (subs?.Subscriptions is { Length: > 0 })
                {
                    await DeleteAllSubs();
                }
            }

            await wsClient.ConnectAsync();
        }
    }

    private async Task WsClientOnWebsocketDisconnected(object sender, EventArgs args)
    {
        await TryReconnect();
    }

    private async Task WsClientOnWebsocketReconnected(object sender, EventArgs args)
    {
        _isWsConnected = true;
        if (tokenService.Token != null)
        {
            await ResubscribeToEventSub();
        }

        await Task.Delay(1000, _cancellationToken);
    }

    private async Task WsClientOnErrorOccurred(object sender, ErrorOccuredArgs args)
    {
        logger.LogException(args.Exception);
        await TryReconnect();
    }

    private async Task WsClientOnWebsocketConnected(object sender, WebsocketConnectedArgs args)
    {
        _isWsConnected = true;
        await ResubscribeToEventSub();
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
            await TryReconnect();
        }
        finally
        {
            WsReconnectSlim.Release();
        }
    }

    private async Task DeleteAllSubs()
    {
        var token = tokenService.Token;
        if (token == null)
        {
            return;
        }

        var response = await GetEventSubsAsync();

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
                    if (await HandleUnauthorizedError($"удаление подписки {subscription.Id}"))
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
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("404") || httpEx.Message.Contains("Not Found"))
                {
                    logger.LogWarning(
                        "Подписка {SubscriptionId} уже удалена или не существует (404)",
                        subscription.Id
                    );
                }
                catch (TwitchLib.Api.Core.Exceptions.BadResourceException)
                {
                    logger.LogWarning(
                        "Подписка {SubscriptionId} недоступна для удаления (BadResourceException)",
                        subscription.Id
                    );
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Ошибка при удалении подписки {SubscriptionId}: {ErrorMessage}",
                        subscription.Id,
                        ex.Message
                    );
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

    private async Task<bool> HandleUnauthorizedError(string operation)
    {
        logger.LogWarning("Получена ошибка 401 при {Operation}. Обновляем токен...", operation);
        var currentToken = tokenService.Token;
        if (currentToken == null)
        {
            logger.LogError("Отсутствует токен в TokenService для {Operation}", operation);
            return false;
        }

        var refreshResult = await tokenService.RefreshTokenAsync(currentToken);
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

    public async Task<string> ResubscribeToEventSub()
    {
        if (SemaphoreSlim.CurrentCount == 0)
        {
            return "Семафор запретил заход";
        }

        await SemaphoreSlim.WaitAsync(_cancellationToken);

        await tokenService.EnsureActualTokenAsync(_cancellationToken);
        var token = tokenService.Token;
        ArgumentException.ThrowIfNullOrWhiteSpace(token?.AccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(token?.RefreshToken);

        try
        {
            await DeleteAllSubs();

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
                if (await HandleUnauthorizedError("создание подписки на channel.raid"))
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
                if (await HandleUnauthorizedError("создание подписки на stream.online"))
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
                if (await HandleUnauthorizedError("создание подписки на stream.offline"))
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
                if (await HandleUnauthorizedError("создание подписки на channel.moderator.add"))
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
                if (await HandleUnauthorizedError("создание подписки на channel.vip.add"))
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
                if (await HandleUnauthorizedError("создание подписки на channel.follow"))
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
                if (await HandleUnauthorizedError("получение списка подписок EventSub"))
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
                var subscriptions = response.Subscriptions.Select(e => e.Type).Distinct().ToArray();
                var message = string.Join(Environment.NewLine, subscriptions);
                await client.SendMessage(
                    TelegramExstension.Rxdcodx,
                    "Подключенные ивенты для твича: " + Environment.NewLine + message,
                    cancellationToken: _cancellationToken
                );
                return $"Реконект EventSub выполнен успешно. Подписки: {string.Join(", ", subscriptions)}";
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

    public async Task<GetEventSubSubscriptionsResponse?> GetEventSubsAsync()
    {
        try
        {
            var token = tokenService.Token;
            return token == null
                ? null
                : await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
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
            var currentToken = tokenService.Token;
            if (currentToken == null)
            {
                logger.LogError("Не удалось обновить токен: токен отсутствует");
                return null;
            }

            var refreshResult = await tokenService.RefreshTokenAsync(currentToken);
            if (refreshResult)
            {
                logger.LogInformation("Токен успешно обновлен, повторяем запрос...");
                // Повторяем запрос с обновленным токеном
                return await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
                    clientId: api.Settings.ClientId,
                    accessToken: tokenService.Token?.AccessToken ?? currentToken.AccessToken
                );
            }
            else
            {
                logger.LogError("Не удалось обновить токен после получения ошибки 401");
                return null;
            }
        }
        catch (HttpRequestException httpEx)
            when (httpEx.Message.Contains("404") || httpEx.Message.Contains("Not Found"))
        {
            logger.LogWarning("EventSub подписки не найдены (404) - возможно, подписок еще нет");
            return null;
        }
        catch (BadResourceException)
        {
            logger.LogWarning("Ресурс EventSub недоступен (BadResourceException)");
            return null;
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
                    await UpdateEventSubAsync();
                },
                stoppingToken
            );
        });

        return Task.CompletedTask;
    }
}
