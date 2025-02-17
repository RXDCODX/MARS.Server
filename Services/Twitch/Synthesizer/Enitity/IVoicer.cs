namespace MARS.Server.Services.Twitch.Synthesizer.Enitity;

public interface IVoicer
{
    void ChangeVolume(int volume);
    void Sound(MessageToSynthezid message);
}
