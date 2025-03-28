namespace MARS.Server.Hubs.Interfaces;

public interface ISoundBarHub
{
    Task Mute(params string[] args);
    Task Unmute();
}
