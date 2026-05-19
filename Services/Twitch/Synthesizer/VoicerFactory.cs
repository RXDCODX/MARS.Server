using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using Microsoft.Extensions.DependencyInjection;

namespace MARS.Server.Services.Twitch.Synthesizer;

public static class VoicerFactory
{
    public static IVoicer CreateVoicer(
        ILogger<IVoicer> logger,
        ITtsVoiceRepository repository,
        IServiceProvider serviceProvider
    )
    {
        return OperatingSystem.IsWindows()
            ? new SyntheziaVoicer(
                logger,
                serviceProvider.GetRequiredService<TtsHubBroadcaster>(),
                serviceProvider
            )
            : new NullVoicer(logger);
    }
}
