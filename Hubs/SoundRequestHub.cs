using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entitys;
using Microsoft.AspNetCore.Mvc;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs;

[SignalRHub(null, AutoDiscover.MethodsAndParams)]
public class SoundRequestHub : Hub<ISoundRequestHub>
{
    private readonly SoundRequestBackendPlayer _player;
    private readonly SoundRequestUserQueue _userQueue;
    private readonly SoundRequestHistoryService _history;

    public SoundRequestHub(SoundRequestBackendPlayer player, SoundRequestUserQueue userQueue, SoundRequestHistoryService history)
    {
        _player = player;
        _userQueue = userQueue;
        _history = history;
    }

    public Task JoinAsClient()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, "client");
    }

    public Task Play() { _player.ResumePlayer(); return Task.CompletedTask; }
    public Task Pause() { _player.PausePlayer(); return Task.CompletedTask; }
    public Task Resume() { _player.ResumePlayer(); return Task.CompletedTask; }
    public Task Stop() { _player.StopPlayer(); return Task.CompletedTask; }
    public Task Skip() { return _player.SkipTrack(); }
    public Task Mute() { _player.MutePlayer(); return Task.CompletedTask; }
    public Task Unmute() { _player.UnmutePlayer(); return Task.CompletedTask; }
    public Task SetVolume(int volume) { _player.SetVolume(volume); return Task.CompletedTask; }

    public async Task AddTrackToQueue(UserRequestedTrack track)
    {
        await _userQueue.AddToQueueAsync(track);
    }

    public async Task<List<UserRequestedTrack>> GetQueue()
    {
        return await _userQueue.GetQueueAsync();
    }

    public async Task<List<BaseTrackInfo>> GetHistory(int count = 20)
    {
        var arr = await _history.GetLastPlayedTracks(count);
        return arr.ToList();
    }

    public Task<PlayerState> GetPlayerState()
    {
        return Task.FromResult(_player.PlayerState);
    }

    public Task Ended(BaseTrackInfo trackInfo)
    {
        // Implementation of Ended method
        return Task.CompletedTask;
    }

    public Task Started(BaseTrackInfo trackInfo)
    {
        // Implementation of Started method
        return Task.CompletedTask;
    }

    public Task ErrorPlaying(BaseTrackInfo trackInfo)
    {
        // Implementation of ErrorPlaying method
        return Task.CompletedTask;
    }
}
