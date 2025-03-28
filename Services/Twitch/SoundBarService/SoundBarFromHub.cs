using MARS.Server.Services.Twitch.SoundBarService.Entitys;

namespace MARS.Server.Services.Twitch.SoundBarService;

public class SoundBarFromHub(IHubContext<SoundBarHub, ISoundBarHub> hubContext) : ISoundBar
{
    public Task Mute(params string[] args)
    {
        return hubContext.Clients.All.Mute(args);
    }

    public Task Unmute()
    {
        return hubContext.Clients.All.Unmute();
    }
}
