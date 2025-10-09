using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;

namespace MARS.Server.Services.SoundRequest;

public class SoundRequestManager(
    IHubContext<SoundRequestHub, ISoundRequestHub> hub,
    IPlayerController player,
    SoundRequestUserQueue queue,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private readonly CancellationToken _token = lifetime.ApplicationStopping;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        player.OnStarted += async info =>
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(_token);
            info.LastTimePlays = DateTime.UtcNow;
            db.SoundRequestBaseTrackInfos.Update(info);
            await db.SaveChangesAsync(_token);
            await hub.Clients.Group("client").PlayerStateChange(player.GetState());
        };

        player.OnEnded += async info =>
        {
            await hub.Clients.Group("client").PlayerStateChange(player.GetState());
        };

        player.OnError += async info =>
        {
            await hub.Clients.Group("client").PlayerStateChange(player.GetState());
        };

        return Task.CompletedTask;
    }

    public PlayerState GetState()
    {
        return player.GetState();
    }

    public Task Pause()
    {
        return player.PauseAsync(_token);
    }

    public Task Resume()
    {
        return player.ResumeAsync(_token);
    }

    public Task Stop()
    {
        return player.StopAsync(_token);
    }

    public Task Skip()
    {
        return player.SkipAsync(_token);
    }

    public Task SetVolume(int volume)
    {
        return player.SetVolumeAsync(volume, _token);
    }

    public Task Mute()
    {
        return player.MuteAsync(_token);
    }

    public Task Unmute()
    {
        return player.UnmuteAsync(_token);
    }

    public async Task AddTrack(UserRequestedTrack track)
    {
        await queue.AddToQueueAsync(track);
    }

    public Task<List<UserRequestedTrack>> GetQueue()
    {
        return queue.GetQueueAsync();
    }
}


