using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.SoundRequest.Entities;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Сервис для управления отправкой уведомлений через SignalR Hub
/// </summary>
public class SignalRService(IHubContext<SoundRequestHub, ISoundRequestHub> hubContext)
{
    /// <summary>
    /// Имя группы для клиентов плеера
    /// </summary>
    public const string PlayerGroupName = "player";

    /// <summary>
    /// Уведомить клиентов об изменении состояния плеера
    /// </summary>
    /// <param name="playerState">Текущее состояние плеера</param>
    public async Task NotifyPlayerStateChangedAsync(PlayerState playerState)
    {
        await hubContext.Clients.Group(PlayerGroupName).PlayerStateChange(playerState);
    }

    /// <summary>
    /// Уведомить клиентов об изменении очереди
    /// </summary>
    /// <param name="queue">Текущая очередь треков</param>
    public async Task NotifyQueueChangedAsync(List<UserRequestedTrack> queue)
    {
        await hubContext.Clients.Group(PlayerGroupName).QueueChanged(queue);
    }

    /// <summary>
    /// Уведомить клиентов о начале воспроизведения трека
    /// </summary>
    public async Task NotifyPlayAsync()
    {
        await hubContext.Clients.Group(PlayerGroupName).Play();
    }

    /// <summary>
    /// Уведомить клиентов о паузе воспроизведения
    /// </summary>
    public async Task NotifyPauseAsync()
    {
        await hubContext.Clients.Group(PlayerGroupName).Pause();
    }

    /// <summary>
    /// Уведомить клиентов о возобновлении воспроизведения
    /// </summary>
    public async Task NotifyResumeAsync()
    {
        await hubContext.Clients.Group(PlayerGroupName).Resume();
    }

    /// <summary>
    /// Уведомить клиентов об остановке воспроизведения
    /// </summary>
    public async Task NotifyStopAsync()
    {
        await hubContext.Clients.Group(PlayerGroupName).Stop();
    }

    /// <summary>
    /// Уведомить клиентов о пропуске трека
    /// </summary>
    public async Task NotifySkipAsync()
    {
        await hubContext.Clients.Group(PlayerGroupName).Skip();
    }

    /// <summary>
    /// Уведомить клиентов об отключении звука
    /// </summary>
    public async Task NotifyMuteAsync()
    {
        await hubContext.Clients.Group(PlayerGroupName).Mute();
    }

    /// <summary>
    /// Уведомить клиентов о включении звука
    /// </summary>
    public async Task NotifyUnmuteAsync()
    {
        await hubContext.Clients.Group(PlayerGroupName).Unmute();
    }

    /// <summary>
    /// Уведомить клиентов об изменении громкости
    /// </summary>
    /// <param name="volume">Новое значение громкости</param>
    public async Task NotifyVolumeChangedAsync(int volume)
    {
        await hubContext.Clients.Group(PlayerGroupName).SetVolume(volume);
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
    public async Task NotifyAllQueueChangedAsync(List<UserRequestedTrack> queue)
    {
        await hubContext.Clients.All.QueueChanged(queue);
    }
}
