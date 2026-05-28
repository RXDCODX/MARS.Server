using System.Collections.Generic;

namespace MARS.Server.Hubs.Interfaces;

/// <summary>
/// Интерфейс для методов, которые СЕРВЕР вызывает на КЛИЕНТЕ (уведомления)
/// Методы, которые КЛИЕНТ вызывает на СЕРВЕРЕ, находятся в самом Hub классе
/// </summary>
public interface ISoundRequestHub
{
    /// <summary>
    /// Уведомление клиентов об изменении состояния плеера
    /// </summary>
    Task PlayerStateChange(PlayerState playerState);

    /// <summary>
    /// Уведомление клиентов об изменении очереди
    /// </summary>
    Task QueueChanged(List<QueueItem> queue);
}
