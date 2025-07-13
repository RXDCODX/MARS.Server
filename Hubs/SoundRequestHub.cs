using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entitys;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs;

[SignalRHub(null, AutoDiscover.MethodsAndParams)]
public class SoundRequestHub(
    SoundRequestBackendPlayer player,
    SoundRequestUserQueue userQueue,
    SoundRequestHistoryService history
) : Hub<ISoundRequestHub>
{
    public Task JoinAsClient() => Groups.AddToGroupAsync(Context.ConnectionId, "client");

    public Task Play()
    {
        player.ResumePlayer();
        return Task.CompletedTask;
    }

    public Task Pause()
    {
        player.PausePlayer();
        return Task.CompletedTask;
    }

    public Task Resume()
    {
        player.ResumePlayer();
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        player.StopPlayer();
        return Task.CompletedTask;
    }

    public Task Skip() => player.SkipTrack();

    public Task Mute()
    {
        player.MutePlayer();
        return Task.CompletedTask;
    }

    public Task Unmute()
    {
        player.UnmutePlayer();
        return Task.CompletedTask;
    }

    public Task SetVolume(int volume)
    {
        player.SetVolume(volume);
        return Task.CompletedTask;
    }

    public async Task AddTrackToQueue(UserRequestedTrack track) =>
        await userQueue.AddToQueueAsync(track);

    public async Task<List<UserRequestedTrack>> GetQueue() => await userQueue.GetQueueAsync();

    public async Task<List<BaseTrackInfo>> GetHistory(int count = 20)
    {
        var arr = await history.GetLastPlayedTracks(count);
        return [.. arr];
    }

    public Task<PlayerState> GetPlayerState() => Task.FromResult(player.PlayerState);

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
