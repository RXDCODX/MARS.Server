using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class NullVoicer(ILogger<IVoicer> logger) : IVoicer
{
    public void ChangeVolume(int volume)
    {
        logger.LogWarning("Changing volume is not supported on this platform.");
    }

    public void Sound(MessageToSynthezid message)
    {
        logger.LogWarning("Speech synthesis is not supported on this platform.");
    }
}
