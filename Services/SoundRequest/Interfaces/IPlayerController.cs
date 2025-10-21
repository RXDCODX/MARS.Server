using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest.Interfaces;

public interface IPlayerController
{
    Task PlayAsync(BaseTrackInfo track, TwitchUser? user, CancellationToken ct);
    Task PauseAsync(CancellationToken ct);
    Task ResumeAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    Task SkipAsync(CancellationToken ct);
    Task SetVolumeAsync(int volume, CancellationToken ct);
    Task MuteAsync(CancellationToken ct);
    Task UnmuteAsync(CancellationToken ct);

    PlayerState GetState();

    event Func<BaseTrackInfo, Task>? OnStarted;
    event Func<BaseTrackInfo, Task>? OnEnded;
    event Func<BaseTrackInfo, Task>? OnError;
}
