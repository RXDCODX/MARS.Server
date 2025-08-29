using Microsoft.Extensions.DependencyInjection;

namespace MARS.Server.Services.Twitch.StreamManagement;

/// <summary>
/// Расширения для регистрации сервисов управления трансляцией Twitch
/// </summary>
public static class TwitchStreamManagementServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет сервисы управления трансляцией Twitch в DI контейнер
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Коллекция сервисов с добавленными сервисами управления трансляцией</returns>
    public static IServiceCollection AddTwitchStreamManagementServices(this IServiceCollection services)
    {
        // Основной сервис управления трансляцией
        services.AddSingleton<TwitchStreamManagementService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchStreamManagementService>());

        // Сервис для обработки команды !title в Twitch чате (смена и получение названия)
        services.AddSingleton<TwitchTitleChangeCommand>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchTitleChangeCommand>());

        return services;
    }

    /// <summary>
    /// Добавляет только основной сервис управления трансляцией (без команд чата)
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Коллекция сервисов с добавленным основным сервисом</returns>
    public static IServiceCollection AddTwitchStreamManagementServiceOnly(this IServiceCollection services)
    {
        services.AddSingleton<TwitchStreamManagementService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchStreamManagementService>());

        return services;
    }
}
