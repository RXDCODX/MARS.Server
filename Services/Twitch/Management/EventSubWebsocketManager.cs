using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Client;

namespace MARS.Server.Services.Twitch.Management;

/// <summary>
/// Управляет жизненным циклом собственного WebSocket клиента для EventSub.
/// Создает и держит единый экземпляр <see cref="WebsocketClient"/> и передает его в
/// <see cref="EventSubWebsocketClient"/>, обеспечивая явное управление состоянием подключения
/// и автоматическое переподключение с ограничением конкуренции.
/// </summary>
public sealed class EventSubWebsocketManager(
    ILogger<EventSubWebsocketManager> logger,
    EventSubWebsocketClient eventSubClient,
    WebsocketClient websocketClient,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private readonly CancellationToken _stopToken = lifetime.ApplicationStopping;
    private static readonly SemaphoreSlim ReconnectSlim = new(1);

    public bool IsConnected => websocketClient is { IsConnected: true, IsFaulted: false };

    public string? SessionId => eventSubClient.SessionId;

    public async Task<bool> EnsureConnectedAsync()
    {
        var result = false;

        if (IsConnected)
        {
            result = true;
        }
        else
        {
            result = await TryReconnectInternalAsync();
        }

        return result;
    }

    public async Task<bool> ReconnectAsync()
    {
        var result = await TryReconnectInternalAsync();
        return result;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Легкий мониторинг состояния для автоподключения
        while (!stoppingToken.IsCancellationRequested && !_stopToken.IsCancellationRequested)
        {
            try
            {
                if (!IsConnected)
                {
                    await TryReconnectInternalAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Ошибка при автопереподключении WebSocket EventSub: {Message}",
                    ex.Message
                );
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch
            {
                // игнор отмены
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (websocketClient.IsConnected || websocketClient.IsFaulted)
            {
                try
                {
                    await websocketClient.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        ex,
                        "Ошибка при остановке (DisconnectAsync): {Message}",
                        ex.Message
                    );
                }
            }
        }
        finally
        {
            websocketClient.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task<bool> TryReconnectInternalAsync()
    {
        var result = false;

        if (ReconnectSlim.CurrentCount == 0)
        {
            return result;
        }

        await ReconnectSlim.WaitAsync(_stopToken);
        try
        {
            // Пробуем мягко переподключить
            if (websocketClient.IsConnected || websocketClient.IsFaulted)
            {
                try
                {
                    await websocketClient.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Ошибка при DisconnectAsync(): {Message}", ex.Message);
                }
            }

            try
            {
                await websocketClient.ConnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка при ConnectAsync(): {Message}", ex.Message);
            }

            // Даем немного времени на установку сессии
            try
            {
                await Task.Delay(1000, _stopToken);
            }
            catch
            {
                // ignore
            }

            if (!IsConnected)
            {
                logger.LogError("Не удалось подключить EventSub WebSocket клиент");
            }

            result = IsConnected;
        }
        finally
        {
            ReconnectSlim.Release();
        }

        return result;
    }
}
