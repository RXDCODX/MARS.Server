using SignalRSwaggerGen.Attributes;

namespace MARS.Server.Hubs.Interfaces;

[SignalRHub]
public interface ISoundBarHub
{
    Task Mute(params string[] args);
    Task Unmute();
}
