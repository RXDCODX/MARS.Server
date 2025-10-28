using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest.Interfaces;

public interface IPlayerController
{
    Task PauseAsync(CancellationToken ct);
    Task ResumeAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    Task SkipAsync(CancellationToken ct);
    Task SetVolumeAsync(float volume, CancellationToken ct);
    Task MuteAsync(CancellationToken ct);
    Task UnmuteAsync(CancellationToken ct);
    Task SetVideoDisplayAsync(VideoDisplay videoDisplay, CancellationToken ct);

    PlayerState GetState();
}
