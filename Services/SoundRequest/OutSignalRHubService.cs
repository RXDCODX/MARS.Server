using MARS.Server.Services.SoundRequest.Entities;

namespace MARS.Server.Services.SoundRequest;

public class OutSignalRHubService
{
    #region IPlayerController Events

    /// <summary>
    /// Событие начала воспроизведения трека
    /// </summary>
    public event Func<BaseTrackInfo, Task>? OnStarted;

    /// <summary>
    /// Событие завершения воспроизведения трека
    /// </summary>
    public event Func<BaseTrackInfo, Task>? OnEnded;

    /// <summary>
    /// Событие ошибки воспроизведения
    /// </summary>
    public event Func<BaseTrackInfo, Task>? OnError;

    #endregion
    public Task OnStartedInvoke(BaseTrackInfo info)
    {
        return OnStarted?.Invoke(info) ?? Task.CompletedTask;
    }

    public Task OnEndedInvoke(BaseTrackInfo info)
    {
        return OnEnded?.Invoke(info) ?? Task.CompletedTask;
    }

    public Task OnErrorInvoke(BaseTrackInfo info)
    {
        return OnError?.Invoke(info) ?? Task.CompletedTask;
    }
}
