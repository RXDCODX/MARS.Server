using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class NullVoicer(ILogger<IVoicer> logger) : IVoicer
{
    public int GetVolume()
    {
        logger.LogWarning("Changing volume is not supported on this platform.");
        return 0;
    }

    public void ChangeVolume(int volume)
    {
        logger.LogWarning("Changing volume is not supported on this platform.");
    }

    public Task Stop()
    {
        return Task.FromResult(
            () => logger.LogWarning("Speech synthesis is not supported on this platform.")
        );
    }

    public Task Sound(MessageToSynthezid message)
    {
        return Task.FromResult(
            () => logger.LogWarning("Speech synthesis is not supported on this platform.")
        );
    }
}
