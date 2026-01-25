using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using MARS.Server.Services.Twitch.Synthesizer.FreeTts;
using MARS.Server.Services.Twitch.Synthesizer.TextProcessing;

namespace MARS.Server.Services.Twitch.Synthesizer;

/// <summary>
/// Extension methods for registering FreeTTS Synthesizer services in DI container
/// </summary>
public static class FreeTtsSynthesizerServiceCollectionExtensions
{
    /// <summary>
    /// Registers all FreeTTS Synthesizer services in the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddFreeTtsSynthesizer(this IServiceCollection services)
    {
        // Register text normalization service as Singleton
        services.AddSingleton<ITextNormalizationService, TextNormalizationService>();

        // Register HTTP client for FreeTTS API
        services
            .AddHttpClient<IFreeTtsHttpClient, FreeTtsHttpClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://freetts.ru/api");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        // Register health check service as Singleton
        services.AddSingleton<IFreeTtsHealthCheckService, FreeTtsHealthCheckService>();

        // Register main synthesizer service as Singleton
        services.AddSingleton<IFreeTtsSynthesizerService, FreeTtsSynthesizerService>();

        return services;
    }

    /// <summary>
    /// Registers FreeTtsVoicer as IVoicer implementation in the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddFreeTtsVoicer(this IServiceCollection services)
    {
        // Ensure FreeTTS Synthesizer services are registered
        services.AddFreeTtsSynthesizer();

        // Register HttpClient for audio playback
        services.AddHttpClient<FreeTtsVoicer>();

        // Register FreeTtsVoicer
        services.AddScoped<FreeTtsVoicer>(serviceProvider =>
        {
            var synthesizerService =
                serviceProvider.GetRequiredService<IFreeTtsSynthesizerService>();
            var voiceRepository = serviceProvider.GetRequiredService<ITtsVoiceRepository>();
            var logger = serviceProvider.GetRequiredService<ILogger<IVoicer>>();
            var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
            var httpClient = serviceProvider.GetRequiredService<HttpClient>();

            return new FreeTtsVoicer(
                synthesizerService,
                voiceRepository,
                logger,
                environment,
                httpClient
            );
        });

        // Optionally register as IVoicer (uncomment if you want to use it as default)
        // services.AddScoped<IVoicer>(sp => sp.GetRequiredService<FreeTtsVoicer>());

        return services;
    }
}

/// <summary>
/// Usage in Program.cs or Startup.cs:
///
/// // Add FreeTTS Synthesizer services
/// builder.Services.AddFreeTtsSynthesizer();
///
/// // Or add with FreeTtsVoicer
/// builder.Services.AddFreeTtsVoicer();
/// </summary>
