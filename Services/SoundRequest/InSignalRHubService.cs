using System.Collections.Generic;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Сервис для управления отправкой уведомлений через SignalR Hub
/// </summary>
public class InSignalRHubService(IHubContext<SoundRequestHub, ISoundRequestHub> hubContext)
{
    /// <summary>
    /// Уведомить клиентов об изменении состояния плеера
    /// </summary>
    /// <param name="playerState">Текущее состояние плеера</param>
    /// <param name="excludeConnectionId">ID соединения, которое нужно исключить из рассылки (например, инициатор изменения)</param>
    public async Task NotifyPlayerStateChangedAsync(
        PlayerState playerState,
        string? excludeConnectionId = null
    )
    {
        if (!string.IsNullOrWhiteSpace(excludeConnectionId))
        {
            await hubContext.Clients.AllExcept(excludeConnectionId).PlayerStateChange(playerState);
        }
        else
        {
            await hubContext.Clients.All.PlayerStateChange(playerState);
        }
    }

    /// <summary>
    /// Уведомить клиентов об изменении очереди
    /// </summary>
    /// <param name="queue">Текущая очередь элементов</param>
    /// <param name="excludeConnectionId">ID соединения, которое нужно исключить из рассылки</param>
    public async Task NotifyQueueChangedAsync(
        List<QueueItem> queue,
        string? excludeConnectionId = null
    )
    {
        if (!string.IsNullOrWhiteSpace(excludeConnectionId))
        {
            await hubContext.Clients.AllExcept(excludeConnectionId).QueueChanged(queue);
        }
        else
        {
            await hubContext.Clients.All.QueueChanged(queue);
        }
    }

    /// <summary>
    /// Уведомить всех клиентов (включая не в группе "player")
    /// </summary>
    /// <param name="playerState">Текущее состояние плеера</param>
    /// <param name="excludeConnectionId">ID соединения, которое нужно исключить из рассылки</param>
    public async Task NotifyAllPlayerStateChangedAsync(
        PlayerState playerState,
        string? excludeConnectionId = null
    )
    {
        if (!string.IsNullOrWhiteSpace(excludeConnectionId))
        {
            await hubContext.Clients.AllExcept(excludeConnectionId).PlayerStateChange(playerState);
        }
        else
        {
            await hubContext.Clients.All.PlayerStateChange(playerState);
        }
    }

    /// <summary>
    /// Уведомить всех клиентов об изменении очереди
    /// </summary>
    /// <param name="queue">Текущая очередь элементов</param>
    /// <param name="excludeConnectionId">ID соединения, которое нужно исключить из рассылки</param>
    public async Task NotifyAllQueueChangedAsync(
        List<QueueItem> queue,
        string? excludeConnectionId = null
    )
    {
        if (!string.IsNullOrWhiteSpace(excludeConnectionId))
        {
            await hubContext.Clients.AllExcept(excludeConnectionId).QueueChanged(queue);
        }
        else
        {
            await hubContext.Clients.All.QueueChanged(queue);
        }
    }
}
