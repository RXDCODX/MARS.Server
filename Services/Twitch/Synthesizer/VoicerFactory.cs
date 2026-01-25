using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using MARS.Server.Services.Twitch.Synthesizer.FreeTts;

namespace MARS.Server.Services.Twitch.Synthesizer;

public static class VoicerFactory
{
    public static IVoicer CreateVoicer(ILogger<IVoicer> logger, ITtsVoiceRepository repository)
    {
        return OperatingSystem.IsWindows()
            ? new SyntheziaVoicer(logger, repository)
            : new NullVoicer(logger);
    }

    public static IVoicer CreateFreeTtsVoicer(
        IFreeTtsSynthesizerService synthesizerService,
        ITtsVoiceRepository repository,
        ILogger<IVoicer> logger,
        IHostEnvironment environment,
        HttpClient httpClient
    )
    {
        return new FreeTtsVoicer(synthesizerService, repository, logger, environment, httpClient);
    }

    public static IVoicer CreateVoicerFromProvider(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<IVoicer>>();
        var repository = serviceProvider.GetRequiredService<ITtsVoiceRepository>();

        // Try to create FreeTTS voicer if service is available
        try
        {
            var synthesizerService = serviceProvider.GetService<IFreeTtsSynthesizerService>();
            if (synthesizerService != null)
            {
                var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
                var httpClient = serviceProvider.GetRequiredService<HttpClient>();
                return CreateFreeTtsVoicer(synthesizerService, repository, logger, environment, httpClient);
            }
        }
        catch
        {
            // Fall back to default voicer
        }

        return CreateVoicer(logger, repository);
    }
}
