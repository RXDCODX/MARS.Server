namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Расширения для регистрации сервиса RxdcodxViewersService
/// </summary>
public static class RxdcodxViewersServiceExtensions
{
    /// <summary>
    /// Добавить сервис RxdcodxViewersService в DI контейнер
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Коллекция сервисов с добавленным сервисом</returns>
    public static IServiceCollection AddRxdcodxViewersService(this IServiceCollection services)
    {
        services.AddScoped<IRxdcodxViewersService, RxdcodxViewersService>();
        return services;
    }

    /// <summary>
    /// Добавить сервис RxdcodxViewersService как синглтон в DI контейнер
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Коллекция сервисов с добавленным сервисом</returns>
    public static IServiceCollection AddRxdcodxViewersServiceAsSingleton(
        this IServiceCollection services
    )
    {
        services.AddSingleton<IRxdcodxViewersService, RxdcodxViewersService>();
        return services;
    }
}
