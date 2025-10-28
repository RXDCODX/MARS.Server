using MARS.Server.Services.SoundRequest.Entities;

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
    public async Task NotifyPlayerStateChangedAsync(PlayerState playerState)
    {
        await hubContext.Clients.All.PlayerStateChange(playerState);
    }

    /// <summary>
    /// Уведомить клиентов об изменении очереди
    /// </summary>
    /// <param name="queue">Текущая очередь элементов</param>
    public async Task NotifyQueueChangedAsync(List<QueueItem> queue)
    {
        await hubContext.Clients.All.QueueChanged(queue);
    }

    /// <summary>
    /// Уведомить всех клиентов (включая не в группе "player")
    /// </summary>
    public async Task NotifyAllPlayerStateChangedAsync(PlayerState playerState)
    {
        await hubContext.Clients.All.PlayerStateChange(playerState);
    }

    /// <summary>
    /// Уведомить всех клиентов об изменении очереди
    /// </summary>
    public async Task NotifyAllQueueChangedAsync(List<QueueItem> queue)
    {
        await hubContext.Clients.All.QueueChanged(queue);
    }
}
