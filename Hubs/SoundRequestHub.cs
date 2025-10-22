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
    public const string MainPlayerName = "mainplayer";
    public const string FramePlayerGroupName = "frameplayer";
    public const string ApiPlayerGroupName = "apiplayer";
    public const string AllPlayers = "all";

    public static readonly List<string> SoundRequestGroups =
    [
        MainPlayerName,
        FramePlayerGroupName,
        ApiPlayerGroupName,
    ];

    public override Task OnConnectedAsync()
    {
        return Clients.Caller.PlayerStateChange(stateManager.GetState());
    }

    public Task Join(string groupName)
    {
        var name = SoundRequestGroups.Find(e =>
            e.Equals(groupName, StringComparison.OrdinalIgnoreCase)
        );

        if (name is not null)
        {
            Groups.AddToGroupAsync(Context.ConnectionId, name);
            Groups.AddToGroupAsync(Context.ConnectionId, AllPlayers);
        }

        return Task.CompletedTask;
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

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var group in SoundRequestGroups)
        {
            Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        return Task.CompletedTask;
    }
}
