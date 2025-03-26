namespace MARS.Server.Services.Twitch.Synthesizer.Enitity;

public interface IVoicer
{
    int GetVolume();
    void ChangeVolume(int volume);
    Task Sound(MessageToSynthezid message);
    Task Stop();
}
