namespace MARS.Server.Services.Twitch.SoundBarService.Entitys;

public interface ISoundBar
{
    public Task Mute(params string[] args);
    public Task Unmute();
}
