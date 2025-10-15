using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs;

[SignalRHub("/hubs/soundrequest", AutoDiscover.MethodsAndParams)]
public class SoundRequestHub(SoundRequestManager manager) : Hub<ISoundRequestHub>
{
    public Task BePlayer() =>
        Groups.AddToGroupAsync(Context.ConnectionId, SignalRService.PlayerGroupName);

    public Task Play()
    {
        _ = manager.ResumeAsync();
        return Task.CompletedTask;
    }

    public Task Pause()
    {
        _ = manager.PauseAsync();
        return Task.CompletedTask;
    }

    public Task Resume()
    {
        _ = manager.ResumeAsync();
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        _ = manager.StopAsync();
        return Task.CompletedTask;
    }

    public Task Skip() => manager.SkipAsync();

    public Task Mute()
    {
        _ = manager.MuteAsync();
        return Task.CompletedTask;
    }

    public Task Unmute()
    {
        _ = manager.UnmuteAsync();
        return Task.CompletedTask;
    }

    public Task SetVolume(int volume)
    {
        _ = manager.SetVolume(volume);
        return Task.CompletedTask;
    }

    public async Task AddTrackToQueue(UserRequestedTrack track) => await manager.AddTrack(track);

    public async Task<List<UserRequestedTrack>> GetQueue() => await manager.GetQueueAsync();

    public async Task<List<BaseTrackInfo>> GetHistory(int count = 20)
    {
        return await manager.GetHistoryAsync(count);
    }

    public Task<PlayerState> GetPlayerState() => Task.FromResult(manager.GetState());

    public async Task PlayNext()
    {
        await manager.PlayNextFromQueueAsync();
    }

    public async Task PlayTrack(Guid trackId)
    {
        await manager.PlayTrackFromQueueAsync(trackId);
    }

    public async Task RemoveTrack(Guid trackId)
    {
        await manager.RemoveTrack(trackId);
    }

    /// <summary>
    /// Вызывается фронтендом когда трек завершил воспроизведение
    /// </summary>
    public async Task Ended()
    {
        await manager.OnTrackEnded();
    }

    /// <summary>
    /// Вызывается фронтендом когда трек начал воспроизведение
    /// </summary>
    public async Task Started()
    {
        await manager.OnTrackStarted();
    }

    /// <summary>
    /// Вызывается фронтендом при ошибке воспроизведения
    /// </summary>
    public async Task ErrorPlaying()
    {
        await manager.OnTrackError();
    }
}
