using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entitys;
using Microsoft.AspNetCore.Mvc;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs;

[SignalRHub(null, AutoDiscover.MethodsAndParams)]
public class SoundRequestHub(SoundRequestSignalREvents events) : Hub<ISoundRequestHub>
{
    public Task JoinAsClient()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, "client");
    }

    public Task Ended([FromBody] BaseTrackInfo trackInfo)
    {
        events.Ended(trackInfo);
        return Task.CompletedTask;
    }

    public Task Started([FromBody] BaseTrackInfo trackInfo)
    {
        events.Started(trackInfo);
        return Task.CompletedTask;
    }

    public Task ErrorPlaying([FromBody] BaseTrackInfo trackInfo)
    {
        events.Error(trackInfo);
        return Task.CompletedTask;
    }
}
