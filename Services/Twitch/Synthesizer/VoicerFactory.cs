using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.Twitch.Synthesizer;

public static class VoicerFactory
{
    public static IVoicer CreateVoicer(ILogger<IVoicer> logger)
    {
        if (OperatingSystem.IsWindows())
        {
            return new SyntheziaVoicer(logger);
        }
        return new NullVoicer(logger);
    }
}
