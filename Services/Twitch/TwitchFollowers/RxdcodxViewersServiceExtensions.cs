namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Расширения для регистрации сервиса RxdcodxViewersService
/// </summary>
public static class RxdcodxViewersServiceExtensions
{
    /// <summary>
    /// Добавить сервис RxdcodxViewersService как синглтон в DI контейнер
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Коллекция сервисов с добавленным сервисом</returns>
    public static IServiceCollection AddRxdcodxViewersServiceAsSingleton(
        this IServiceCollection services
    )
    {
        // Регистрируем вспомогательные сервисы
        services.AddScoped<FollowerDbService>();
        services.AddScoped<TwitchUserInfoService>();
        
        // Регистрируем основной сервис
        services.AddSingleton<RxdcodxViewersService>();
        services.AddSingleton<IRxdcodxViewersService>(sp =>
            sp.GetRequiredService<RxdcodxViewersService>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<RxdcodxViewersService>());
        return services;
    }
}
