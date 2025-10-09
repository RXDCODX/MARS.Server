using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs;

[SignalRHub("/hubs/soundrequest", AutoDiscover.MethodsAndParams)]
public class SoundRequestHub(
    SoundRequestManager manager
) : Hub<ISoundRequestHub>
{
    public Task JoinAsClient() => Groups.AddToGroupAsync(Context.ConnectionId, "client");

    public Task Play()
    {
        _ = manager.Resume();
        return Task.CompletedTask;
    }

    public Task Pause()
    {
        _ = manager.Pause();
        return Task.CompletedTask;
    }

    public Task Resume()
    {
        _ = manager.Resume();
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        _ = manager.Stop();
        return Task.CompletedTask;
    }

    public Task Skip() => manager.Skip();

    public Task Mute()
    {
        _ = manager.Mute();
        return Task.CompletedTask;
    }

    public Task Unmute()
    {
        _ = manager.Unmute();
        return Task.CompletedTask;
    }

    public Task SetVolume(int volume)
    {
        _ = manager.SetVolume(volume);
        return Task.CompletedTask;
    }

    public async Task AddTrackToQueue(UserRequestedTrack track) =>
        await manager.AddTrack(track);

    public async Task<List<UserRequestedTrack>> GetQueue() => await manager.GetQueue();

    public Task<List<BaseTrackInfo>> GetHistory(int count = 20)
    {
        return Task.FromResult(new List<BaseTrackInfo>());
    }

    public Task<PlayerState> GetPlayerState() => Task.FromResult(manager.GetState());

    public Task Ended() =>
        // Implementation of Ended method
        Task.CompletedTask;

    public Task Started() =>
        // Implementation of Started method
        Task.CompletedTask;

    public Task ErrorPlaying() =>
        // Implementation of ErrorPlaying method
        Task.CompletedTask;
}
