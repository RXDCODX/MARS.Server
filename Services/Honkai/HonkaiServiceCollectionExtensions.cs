using Microsoft.Extensions.DependencyInjection;

namespace MARS.Server.Services.Honkai;

public static class HonkaiServiceCollectionExtensions
{
    public static IServiceCollection AddHonkaiServices(this IServiceCollection services)
    {
        services.AddSingleton<DailyMarkRedeemService>();
        services.AddHostedService(sp => sp.GetRequiredService<DailyMarkRedeemService>());

        services.AddSingleton<EnergyNotificationService>();
        services.AddHostedService(sp => sp.GetRequiredService<EnergyNotificationService>());

        services.AddSingleton<IHonkaiApiService, HonkaiApiService>();
        services.AddSingleton<IHonkaiNotificationService, HonkaiNotificationService>();

        return services;
    }
}
