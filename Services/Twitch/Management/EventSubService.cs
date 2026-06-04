using System.Diagnostics;
using System.Timers;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets.Core.EventArgs;

namespace MARS.Server.Services.Twitch.Management;

[DebuggerNonUserCode]
public class EventSubService(
    ITwitchAPI api,
    ILogger<EventSubService> logger,
    ITelegramBotClient client,
    TokenService tokenService,
    IHostApplicationLifetime lifetime,
    EventSubWebsocketClient wsClient
) : BackgroundService
{
    private static readonly Timer EventTimer = new(TimeSpan.FromMinutes(5)) { AutoReset = true };
    private static readonly SemaphoreSlim SemaphoreSlim = new(1);
    private static readonly SemaphoreSlim WebsocketSemaphoreSlim = new(1);
    private static readonly SemaphoreSlim WebsocketConnectSemaphoreSlim = new(1);

    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;
    private volatile bool _firstActivation = true;

    public async Task UpdateEventSubAsync()
    {
        if (!_firstActivation)
        {
            if (tokenService.Token != null)
            {
                var subs = await GetEventSubsAsync();
                var hasActiveSubscriptions =
                    subs != null
                    && subs.Subscriptions.Any(e =>
                        e.Status.Equals("enabled", StringComparison.OrdinalIgnoreCase)
                    );
                if (!hasActiveSubscriptions)
                {
                    await ResubscribeToEventSubAsync();
                }
            }
        }

        if (_firstActivation)
        {
            wsClient.WebsocketConnected += WsClientOnWebsocketConnected;
            wsClient.WebsocketDisconnected += WsClientOnWebsocketDisconnected;
            wsClient.WebsocketReconnected += WsClientOnWebsocketReconnected;
            wsClient.ErrorOccurred += WsClientOnErrorOccurred;

            if (tokenService.Token != null)
            {
                var subs = await GetEventSubsAsync();
                if (subs?.Subscriptions is { Length: > 0 })
                {
                    await DeleteAllSubsAsync();
                }
            }

            _firstActivation = false;
            _ = Task.Factory.StartNew(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(wsClient.SessionId))
                    {
                        await SafeConnectAsync();
                    }

                    await ResubscribeToEventSubAsync();
                },
                _cancellationToken
            );
        }
    }

    private async Task SafeConnectAsync()
    {
        var lockTaken = false;

        try
        {
            await WebsocketConnectSemaphoreSlim.WaitAsync(_cancellationToken);
            lockTaken = true;

            if (string.IsNullOrWhiteSpace(wsClient.SessionId))
            {
                await wsClient.ConnectAsync();
            }
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("already been started", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                ex,
                "Пропущена дублирующая попытка ConnectAsync: WebSocket уже запускается"
            );
        }
        catch (OperationCanceledException)
        {
            // graceful cancellation
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
        finally
        {
            if (lockTaken)
            {
                WebsocketConnectSemaphoreSlim.Release();
            }
        }
    }

    private async Task WsClientOnWebsocketConnected(object? sender, WebsocketConnectedArgs args)
    {
        await ResubscribeToEventSubAsync();
    }

    private async Task WsClientOnWebsocketDisconnected(object? sender, EventArgs args)
    {
        await TryReconnectWithBackoffAsync();
    }

    private async Task WsClientOnWebsocketReconnected(object? sender, EventArgs args)
    {
        if (tokenService.Token != null)
        {
            await ResubscribeToEventSubAsync();
        }
        await Task.Delay(500, _cancellationToken);
    }

    private async Task WsClientOnErrorOccurred(object? sender, ErrorOccuredArgs args)
    {
        logger.LogException(args.Exception);
        await TryReconnectWithBackoffAsync();
    }

    private async Task TryReconnectWithBackoffAsync()
    {
        var lockTaken = false;

        if (WebsocketSemaphoreSlim.CurrentCount == 0)
        {
            return;
        }

        try
        {
            await WebsocketSemaphoreSlim.WaitAsync(_cancellationToken);
            lockTaken = true;

            var delayMs = 500;
            for (
                var attempt = 0;
                attempt < 5 && !_cancellationToken.IsCancellationRequested;
                attempt++
            )
            {
                try
                {
                    var reconnected = await wsClient.ReconnectAsync();
                    if (reconnected)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "WebSocket реконнект не удался (попытка {Attempt})",
                        attempt + 1
                    );
                }

                await Task.Delay(delayMs, _cancellationToken);
                delayMs = Math.Min(delayMs * 2, 8000);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful cancellation
        }
        finally
        {
            if (lockTaken)
            {
                WebsocketSemaphoreSlim.Release();
            }
        }
    }

    private async Task DeleteAllSubsAsync()
    {
        if (tokenService.Token != null)
        {
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
                            tokenService.Token.AccessToken
                        );
                    }
                    catch (HttpRequestException httpEx)
                        when (httpEx.Message.Contains("401")
                            || httpEx.Message.Contains("Unauthorized")
                        )
                    {
                        var refreshed = await HandleUnauthorizedError(
                            $"удаление подписки {subscription.Id}"
                        );
                        if (refreshed)
                        {
                            await api.Helix.EventSub.DeleteEventSubSubscriptionAsync(
                                subscription.Id,
                                api.Settings.ClientId,
                                tokenService.Token.AccessToken
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
                        when (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden")
                        )
                    {
                        logger.LogError(
                            "Отказано в доступе (403) при удалении подписки {SubscriptionId}: {Error}",
                            subscription.Id,
                            httpEx.Message
                        );
                    }
                    catch (HttpRequestException httpEx)
                        when (httpEx.Message.Contains("404") || httpEx.Message.Contains("Not Found")
                        )
                    {
                        logger.LogWarning(
                            "Подписка {SubscriptionId} уже удалена или не существует (404)",
                            subscription.Id
                        );
                    }
                    catch (BadResourceException)
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
    }

    private async Task<bool> EnsureWebSocketConnectedAsync()
    {
        var result = false;
        var lockTaken = false;

        try
        {
            await WebsocketConnectSemaphoreSlim.WaitAsync(_cancellationToken);
            lockTaken = true;

            result = await wsClient.ReconnectAsync();
            if (!result)
            {
                logger.LogError("Не удалось подключить WebSocket для создания подписок");
            }
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("already been started", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                ex,
                "Пропущена дублирующая попытка ReconnectAsync: WebSocket уже запускается"
            );
            result = !string.IsNullOrWhiteSpace(wsClient.SessionId);
        }
        catch (OperationCanceledException)
        {
            // graceful cancellation
            result = false;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = false;
        }
        finally
        {
            if (lockTaken)
            {
                WebsocketConnectSemaphoreSlim.Release();
            }
        }

        return result;
    }

    private async Task<bool> HandleUnauthorizedError(string operation)
    {
        logger.LogWarning("Получена ошибка 401 при {Operation}. Обновляем токен...", operation);
        bool result;
        if (tokenService.Token != null)
        {
            var refreshResult = await tokenService.RefreshTokenAsync(tokenService.Token);
            if (refreshResult)
            {
                logger.LogInformation("Токен успешно обновлен для {Operation}", operation);
                result = true;
            }
            else
            {
                logger.LogError("Не удалось обновить токен для {Operation}", operation);
                result = false;
            }
        }
        else
        {
            logger.LogError("Отсутствует токен в TokenService для {Operation}", operation);
            result = false;
        }

        return result;
    }

    public async Task<string> ResubscribeToEventSubAsync()
    {
        var result = string.Empty;
        if (SemaphoreSlim.CurrentCount == 0)
        {
            result = "Семафор запретил заход";
            return result;
        }

        await SemaphoreSlim.WaitAsync(_cancellationToken);
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tokenService.Token?.AccessToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(tokenService.Token?.RefreshToken);
            await DeleteAllSubsAsync();

            var wsOk = await EnsureWebSocketConnectedAsync();
            if (wsOk)
            {
                var condition = new Dictionary<string, string>
                {
                    { "to_broadcaster_user_id", TwitchExstension.ChannelId },
                };

                // channel.raid
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
                        null,
                        api.Settings.ClientId,
                        tokenService.Token!.AccessToken
                    );
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    var refreshed = await HandleUnauthorizedError(
                        "создание подписки на channel.raid"
                    );
                    if (refreshed)
                    {
                        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "channel.raid",
                            "1",
                            condition,
                            EventSubTransportMethod.Websocket,
                            wsClient.SessionId,
                            null,
                            null,
                            null,
                            api.Settings.ClientId,
                            tokenService.Token!.AccessToken
                        );
                    }
                    else
                    {
                        result =
                            "Ошибка: не удалось обновить токен для создания подписки на channel.raid";
                    }
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden"))
                {
                    result = "Ошибка: нет доступа (403) при создании подписки на channel.raid";
                }

                // stream.online
                condition.Clear();
                condition.Add("broadcaster_user_id", TwitchExstension.ChannelId);
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
                        null,
                        api.Settings.ClientId,
                        tokenService.Token!.AccessToken
                    );
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    var refreshed = await HandleUnauthorizedError(
                        "создание подписки на stream.online"
                    );
                    if (refreshed)
                    {
                        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "stream.online",
                            "1",
                            condition,
                            EventSubTransportMethod.Websocket,
                            wsClient.SessionId,
                            null,
                            null,
                            null,
                            api.Settings.ClientId,
                            tokenService.Token!.AccessToken
                        );
                    }
                    else
                    {
                        result =
                            "Ошибка: не удалось обновить токен для создания подписки на stream.online";
                    }
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden"))
                {
                    result = "Ошибка: нет доступа (403) при создании подписки на stream.online";
                }

                // stream.offline
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
                        null,
                        api.Settings.ClientId,
                        tokenService.Token!.AccessToken
                    );
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    var refreshed = await HandleUnauthorizedError(
                        "создание подписки на stream.offline"
                    );
                    if (refreshed)
                    {
                        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "stream.offline",
                            "1",
                            condition,
                            EventSubTransportMethod.Websocket,
                            wsClient.SessionId,
                            null,
                            null,
                            null,
                            api.Settings.ClientId,
                            tokenService.Token!.AccessToken
                        );
                    }
                    else
                    {
                        result =
                            "Ошибка: не удалось обновить токен для создания подписки на stream.offline";
                    }
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden"))
                {
                    result = "Ошибка: нет доступа (403) при создании подписки на stream.offline";
                }

                // channel.channel_points_custom_reward_redemption.add
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
                        null,
                        api.Settings.ClientId,
                        tokenService.Token!.AccessToken
                    );
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    var refreshed = await HandleUnauthorizedError(
                        "создание подписки на channel.channel_points_custom_reward_redemption.add"
                    );
                    if (refreshed)
                    {
                        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "channel.channel_points_custom_reward_redemption.add",
                            "1",
                            condition,
                            EventSubTransportMethod.Websocket,
                            wsClient.SessionId,
                            null,
                            null,
                            null,
                            api.Settings.ClientId,
                            tokenService.Token!.AccessToken
                        );
                    }
                    else
                    {
                        result =
                            "Ошибка: не удалось обновить токен для создания подписки на channel.channel_points_custom_reward_redemption.add";
                    }
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden"))
                {
                    result =
                        "Ошибка: нет доступа (403) при создании подписки на channel.channel_points_custom_reward_redemption.add";
                }

                // channel.moderator.add
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
                        null,
                        api.Settings.ClientId,
                        tokenService.Token!.AccessToken
                    );
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    var refreshed = await HandleUnauthorizedError(
                        "создание подписки на channel.moderator.add"
                    );
                    if (refreshed)
                    {
                        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "channel.moderator.add",
                            "1",
                            condition,
                            EventSubTransportMethod.Websocket,
                            wsClient.SessionId,
                            null,
                            null,
                            null,
                            api.Settings.ClientId,
                            tokenService.Token!.AccessToken
                        );
                    }
                    else
                    {
                        result =
                            "Ошибка: не удалось обновить токен для создания подписки на channel.moderator.add";
                    }
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden"))
                {
                    result =
                        "Ошибка: нет доступа (403) при создании подписки на channel.moderator.add";
                }

                // channel.vip.add
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
                        null,
                        api.Settings.ClientId,
                        tokenService.Token!.AccessToken
                    );
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    var refreshed = await HandleUnauthorizedError(
                        "создание подписки на channel.vip.add"
                    );
                    if (refreshed)
                    {
                        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "channel.vip.add",
                            "1",
                            condition,
                            EventSubTransportMethod.Websocket,
                            wsClient.SessionId,
                            null,
                            null,
                            null,
                            api.Settings.ClientId,
                            tokenService.Token!.AccessToken
                        );
                    }
                    else
                    {
                        result =
                            "Ошибка: не удалось обновить токен для создания подписки на channel.vip.add";
                    }
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden"))
                {
                    result = "Ошибка: нет доступа (403) при создании подписки на channel.vip.add";
                }

                // channel.follow v2 (требует broadcaster + moderator)
                condition.Add("moderator_user_id", TwitchExstension.ChannelId);
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
                        null,
                        api.Settings.ClientId,
                        tokenService.Token!.AccessToken
                    );
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    var refreshed = await HandleUnauthorizedError(
                        "создание подписки на channel.follow"
                    );
                    if (refreshed)
                    {
                        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "channel.follow",
                            "2",
                            condition,
                            EventSubTransportMethod.Websocket,
                            wsClient.SessionId,
                            null,
                            null,
                            null,
                            api.Settings.ClientId,
                            tokenService.Token!.AccessToken
                        );
                    }
                    else
                    {
                        result =
                            "Ошибка: не удалось обновить токен для создания подписки на channel.follow";
                    }
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden"))
                {
                    result = "Ошибка: нет доступа (403) при создании подписки на channel.follow";
                }

                // Проверяем итог
                GetEventSubSubscriptionsResponse? response = null;
                try
                {
                    response = await api
                        .Helix.EventSub.GetEventSubSubscriptionsAsync(
                            new GetEventSubSubscriptionsRequest(),
                            api.Settings.ClientId,
                            tokenService.Token!.AccessToken
                        )
                        .ConfigureAwait(false);
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    var refreshed = await HandleUnauthorizedError(
                        "получение списка подписок EventSub"
                    );
                    if (refreshed)
                    {
                        response = await api
                            .Helix.EventSub.GetEventSubSubscriptionsAsync(
                                new GetEventSubSubscriptionsRequest(),
                                api.Settings.ClientId,
                                tokenService.Token!.AccessToken
                            )
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        result =
                            "Ошибка: не удалось обновить токен для получения списка подписок EventSub";
                    }
                }
                catch (HttpRequestException httpEx)
                    when (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden"))
                {
                    result = "Ошибка: нет доступа (403) при получении списка подписок EventSub";
                }

                if (response == null || response.Subscriptions.Length < 1)
                {
                    logger.LogError("Не получилось подписать EventSub");
                    result = "Ошибка: не удалось подписаться на EventSub";
                }
                else
                {
                    var subscriptions = response
                        .Subscriptions.Select(e => e.Type)
                        .Distinct()
                        .ToArray();
                    var message = string.Join(Environment.NewLine, subscriptions);
                    await client.SendMessage(
                        TelegramExstension.Rxdcodx,
                        "Подключенные ивенты для твича: " + Environment.NewLine + message,
                        cancellationToken: _cancellationToken
                    );
                    result =
                        $"Реконект EventSub выполнен успешно. Подписки: {string.Join(", ", subscriptions)}";
                }
            }
            else
            {
                result = "Ошибка: не удалось подключить WebSocket для создания подписок";
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = $"Ошибка при реконекте EventSub: {ex.Message}";
        }
        finally
        {
            SemaphoreSlim.Release(1);
        }

        return result;
    }

    public async Task<GetEventSubSubscriptionsResponse?> GetEventSubsAsync()
    {
        GetEventSubSubscriptionsResponse? result = null;
        try
        {
            if (tokenService.Token != null)
            {
                result = await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
                    new GetEventSubSubscriptionsRequest(),
                    api.Settings.ClientId,
                    tokenService.Token.AccessToken
                );
            }
        }
        catch (HttpRequestException httpEx)
            when (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
        {
            logger.LogWarning(
                "Получена ошибка 401 (Unauthorized) при получении EventSub подписок. Пытаемся обновить токен..."
            );
            if (tokenService.Token != null)
            {
                var refreshResult = await tokenService.RefreshTokenAsync(tokenService.Token);
                if (refreshResult)
                {
                    logger.LogInformation("Токен успешно обновлен, повторяем запрос...");
                    result = await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
                        new GetEventSubSubscriptionsRequest(),
                        api.Settings.ClientId,
                        tokenService.Token?.AccessToken
                    );
                }
                else
                {
                    logger.LogError("Не удалось обновить токен после получения ошибки 401");
                    result = null;
                }
            }
            else
            {
                logger.LogError("Не удалось обновить токен: токен отсутствует");
                result = null;
            }
        }
        catch (HttpRequestException httpEx)
            when (httpEx.Message.Contains("404") || httpEx.Message.Contains("Not Found"))
        {
            logger.LogWarning("EventSub подписки не найдены (404) - возможно, подписок еще нет");
            result = null;
        }
        catch (BadResourceException)
        {
            logger.LogWarning("Ресурс EventSub недоступен (BadResourceException)");
            result = null;
        }
        catch (Exception e)
        {
            logger.LogException(e);
            result = null;
        }

        return result;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var result = Task.CompletedTask;
        lifetime.ApplicationStarted.Register(() =>
        {
            Task.Factory.StartNew(
                async () =>
                {
                    await UpdateEventSubAsync();
                },
                stoppingToken
            );
            EventTimer.Elapsed += EventTimerOnElapsed;
            EventTimer.Start();
        });
        return result;
    }

    private async void EventTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        await Task.Factory.StartNew(
            async () =>
            {
                var subs = await GetEventSubsAsync();
                var isEnabled = subs?.Subscriptions.Any(t => t.Status.Equals("enabled"));
                if (!isEnabled ?? false)
                {
                    await ResubscribeToEventSubAsync();
                }
            },
            _cancellationToken
        );
    }
}
