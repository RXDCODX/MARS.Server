using MARS.Server.Services.SoundRequest.Entitys;

namespace MARS.Server.Services.SoundRequest;

public delegate Task PlayerAction(BaseTrackInfo trackInfo);

public class SoundRequestSignalREvents
{
    public event PlayerAction EndedEvent = info => Task.CompletedTask;
    public event PlayerAction StartedEvent = info => Task.CompletedTask;
    public event PlayerAction ErrorEvent = info => Task.CompletedTask;

    public Task Ended(BaseTrackInfo trackInfo)
    {
        EndedEvent.Invoke(trackInfo);
        return Task.CompletedTask;
    }

    public Task Started(BaseTrackInfo trackInfo)
    {
        StartedEvent.Invoke(trackInfo);
        return Task.CompletedTask;
    }

    public Task Error(BaseTrackInfo trackInfo)
    {
        ErrorEvent.Invoke(trackInfo);
        return Task.CompletedTask;
    }
}
