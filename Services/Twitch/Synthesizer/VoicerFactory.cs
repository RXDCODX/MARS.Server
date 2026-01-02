using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.Twitch.Synthesizer;

public static class VoicerFactory
{
    public static IVoicer CreateVoicer(ILogger<IVoicer> logger, ITtsVoiceRepository repository)
    {
        return OperatingSystem.IsWindows()
            ? new SyntheziaVoicer(logger, repository)
            : new NullVoicer(logger);
    }
}
