using MARS.Server.Services.SoundRequest.Entitys;

namespace MARS.Server.Hubs.Interfaces;

public interface ISoundRequestHub
{
    public Task PlayerStateChange(PlayerState playerState);
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
