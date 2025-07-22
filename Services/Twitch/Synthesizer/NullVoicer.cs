using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class NullVoicer(ILogger<IVoicer> logger) : IVoicer
{
    public bool IsActive { get; set; }

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

    public Task Block()
    {
        return Task.FromResult(
            () => logger.LogWarning("Speech block is not supported on this platform.")
        );
    }

    public Task Unlock()
    {
        return Task.FromResult(
            () => logger.LogWarning("Speech unblock is not supported on this platform.")
        );
    }

    public Task Sound(MessageToSynthezid message)
    {
        return Task.FromResult(
            () => logger.LogWarning("Speech synthesis is not supported on this platform.")
        );
    }
}
