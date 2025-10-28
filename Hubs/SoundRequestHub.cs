using System.Configuration;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;
using StateManager = MARS.Server.Services.SoundRequest.StateManager;

namespace MARS.Server.Hubs;

[SignalRHub("/hubs/soundrequest", AutoDiscover.MethodsAndParams)]
public class SoundRequestHub(OutSignalRHubService service, StateManager stateManager)
    : Hub<ISoundRequestHub>
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

    public Task VolumeChange(float volume)
    {
        return stateManager.SetVolumeAsync(volume);
    }

    /// <summary>
    /// Вызывается фронтендом для обновления прогресса воспроизведения трека
    /// </summary>
    /// <param name="seconds">Текущая позиция воспроизведения в секундах</param>
    public Task TrackProgress(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return stateManager.UpdateCurrentTrackProgressAsync(span, notify: false);
    }
}
