using MARS.Server.Services.SoundRequest.Entities;

namespace MARS.Server.Hubs.Interfaces;

public interface ISoundRequestHub
{
    /// <summary>
    /// Уведомление клиентов об изменении состояния плеера
    /// </summary>
    Task PlayerStateChange(PlayerState playerState);

    /// <summary>
    /// Уведомление клиентов об изменении очереди
    /// </summary>
    Task QueueChanged(List<UserRequestedTrack> queue);

    Task Play();
    Task Pause();
    Task Resume();
    Task Stop();
    Task Skip();
    Task Mute();
    Task Unmute();
    Task SetVolume(int volume);
    Task AddTrackToQueue(UserRequestedTrack track);
    Task<List<UserRequestedTrack>> GetQueue();
    Task<List<BaseTrackInfo>> GetHistory(int count = 20);
    Task<PlayerState> GetPlayerState();
}
