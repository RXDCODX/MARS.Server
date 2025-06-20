using MARS.Server.Services.SoundRequest.Entitys;

namespace MARS.Server.Hubs.Interfaces;

public interface ISoundRequestHub
{
    public Task PlayerStateChange(PlayerState playerState);
}
