using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;
using StateManager = MARS.Server.Services.SoundRequest.StateManager;

namespace MARS.Server.Hubs;

[SignalRHub("/hubs/soundrequest", AutoDiscover.MethodsAndParams)]
public class SoundRequestHub(
    OutSignalRHubService service,
    StateManager stateManager,
    MainPlayer mainPlayer
) : Hub<ISoundRequestHub>
{
    /// <summary>
    /// Имя группы для клиентов плеера
    /// </summary>
    public override Task OnConnectedAsync()
    {
        return Clients.Caller.PlayerStateChange(stateManager.GetState());
    }

    /// <summary>
    /// Вызывается фронтендом когда трек завершил воспроизведение
    /// </summary>
    public Task Ended(BaseTrackInfo info)
    {
        return service.OnEndedInvoke(info);
    }

    /// <summary>
    /// Вызывается фронтендом когда трек начал воспроизведение
    /// </summary>
    public Task Started(BaseTrackInfo info)
    {
        return service.OnStartedInvoke(info);
    }

    /// <summary>
    /// Вызывается фронтендом при ошибке воспроизведения
    /// </summary>
    public Task ErrorPlaying(BaseTrackInfo info)
    {
        return service.OnErrorInvoke(info);
    }

    /// <summary>
    /// Вызывается фронтендом при изменении состояния плеера (включая громкость, паузу, воспроизведение и т.д.)
    /// Рассылает новое состояние всем клиентам кроме отправителя
    /// </summary>
    /// <param name="newState">Новое состояние плеера от фронтенда</param>
    public async Task FrontStateChange(PlayerState newState)
    {
        // Если фронтенд пытается запустить воспроизведение (Play),
        // проверяем, что текущий трек загружен
        if (newState.State == PlaybackState.Playing)
        {
            await mainPlayer.EnsureCurrentQueueItemLoadedAsync();
        }

        // Обновляем состояние на сервере
        await stateManager.UpdateStateAsync(state =>
        {
            state.State = newState.State;
            state.IsMuted = newState.IsMuted;
            state.Volume = newState.Volume;
            state.VideoState = newState.VideoState;
            state.CurrentTrackProgress = newState.CurrentTrackProgress;
        });
    }

    /// <summary>
    /// Вызывается фронтендом для обновления прогресса воспроизведения трека
    /// </summary>
    /// <param name="seconds">Текущая позиция воспроизведения в секундах</param>
    public Task TrackProgress(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return stateManager.UpdateCurrentTrackProgressAsync(
            span,
            notify: false,
            excludeConnectionId: Context.ConnectionId
        );
    }

    /// <summary>
    /// Вызывается фронтендом для переключения на следующий трек
    /// </summary>
    public Task SkipTrack()
    {
        return mainPlayer.SkipAsync(CancellationToken.None);
    }

    /// <summary>
    /// Вызывается фронтендом для переключения на предыдущий трек из истории
    /// </summary>
    public Task PlayPrevious()
    {
        return mainPlayer.PlayPreviousFromHistoryAsync();
    }
}
