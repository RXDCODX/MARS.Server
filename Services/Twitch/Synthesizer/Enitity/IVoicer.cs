namespace MARS.Server.Services.Twitch.Synthesizer.Enitity;

public interface IVoicer
{
    bool IsActive { get; set; }
    int GetVolume();
    void ChangeVolume(int volume);
    Task Sound(MessageToSynthezid message);
    Task Stop();
    Task Block();
    Task Unlock();
}
