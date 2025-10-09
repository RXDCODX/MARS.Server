using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;

namespace MARS.Server.Services.SoundRequest.Player;

public class FakeMediaPlayer : IPlayerController
{
    private readonly PlayerState _state;
    private CancellationTokenSource _cts = new();

    public FakeMediaPlayer()
    {
        _state = new PlayerState { IsPaused = true, IsStoped = true, Volume = 100 };
    }

    public event Func<BaseTrackInfo, Task>? OnStarted;
    public event Func<BaseTrackInfo, Task>? OnEnded;
    public event Func<BaseTrackInfo, Task>? OnError;

    public PlayerState GetState()
    {
        return _state;
    }

    public async Task PlayAsync(BaseTrackInfo track, CancellationToken ct)
    {
        _cts.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        _state.UpdatePlayerState(s =>
        {
            s.CurrentTrack = track;
            s.IsPaused = false;
            s.IsStoped = false;
            s.CurrentTrackDuration = track.Duration;
        });

        if (OnStarted != null)
        {
            await OnStarted.Invoke(track);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested && _state.CurrentTrack != null)
                {
                    await Task.Delay(1000, token);
                    if (_state.IsPaused || _state.IsStoped)
                    {
                        continue;
                    }

                    if (_state.CurrentTrackDuration.HasValue)
                    {
                        var next = _state.CurrentTrackDuration.Value.Subtract(TimeSpan.FromSeconds(1));
                        _state.UpdatePlayerState(s => s.CurrentTrackDuration = next);
                        if (next <= TimeSpan.Zero)
                        {
                            break;
                        }
                    }
                }

                var finished = _state.CurrentTrack;
                if (finished != null && OnEnded != null)
                {
                    await OnEnded.Invoke(finished);
                }
            }
            catch (Exception)
            {
                if (_state.CurrentTrack != null && OnError != null)
                {
                    await OnError.Invoke(_state.CurrentTrack);
                }
            }
        }, token);
    }

    public Task PauseAsync(CancellationToken ct)
    {
        _state.UpdatePlayerState(s => s.IsPaused = true);
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken ct)
    {
        _state.UpdatePlayerState(s => s.IsPaused = false);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _cts.Cancel();
        _state.UpdatePlayerState(s =>
        {
            s.CurrentTrack = null;
            s.NextTrack = null;
            s.CurrentTrackDuration = null;
            s.IsStoped = true;
            s.IsPaused = false;
        });
        return Task.CompletedTask;
    }

    public Task SkipAsync(CancellationToken ct)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    public Task SetVolumeAsync(int volume, CancellationToken ct)
    {
        if (volume < 0 || volume > 100)
        {
            return Task.CompletedTask;
        }
        _state.UpdatePlayerState(s => s.Volume = volume);
        return Task.CompletedTask;
    }

    public Task MuteAsync(CancellationToken ct)
    {
        _state.UpdatePlayerState(s => s.IsMuted = true);
        return Task.CompletedTask;
    }

    public Task UnmuteAsync(CancellationToken ct)
    {
        _state.UpdatePlayerState(s => s.IsMuted = false);
        return Task.CompletedTask;
    }
}


