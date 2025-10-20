using MARS.Server.Services.SoundRequest;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs;

[SignalRHub("/hubs/soundrequest", AutoDiscover.MethodsAndParams)]
public class SoundRequestHub(SoundRequestManager manager) : Hub<ISoundRequestHub>
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
    public async Task Ended()
    {
        await manager.OnTrackEnded();
    }

    /// <summary>
    /// Вызывается фронтендом когда трек начал воспроизведение
    /// </summary>
    public async Task Started()
    {
        await manager.OnTrackStarted();
    }

    /// <summary>
    /// Вызывается фронтендом при ошибке воспроизведения
    /// </summary>
    public async Task ErrorPlaying()
    {
        await manager.OnTrackError();
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
