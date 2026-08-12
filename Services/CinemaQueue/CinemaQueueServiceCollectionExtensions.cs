using MARS.Server.Services.CinemaQueue.Interfaces;
using MARS.Server.Services.CinemaQueue.Repositories;
using MARS.Server.Services.CinemaQueue.Services;

namespace MARS.Server.Services.CinemaQueue;

public static class CinemaQueueServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет сервисы CinemaQueue в коллекцию сервисов
    /// </summary>
    public static IServiceCollection AddCinemaQueueServices(this IServiceCollection services)
    {
        // Регистрируем репозиторий
        services.AddScoped<ICinemaQueueRepository, CinemaQueueRepository>();

        // Регистрируем основной сервис
        services.AddScoped<ICinemaQueueService, CinemaQueueService>();

        // Регистрируем Twitch интеграцию как BackgroundService
        services.AddHostedService<TwitchCinemaQueueService>();

        return services;
    }

    /// <summary>
    /// Добавляет сервисы CinemaQueue как Singleton
    /// </summary>
    public static IServiceCollection AddCinemaQueueServicesAsSingleton(
        this IServiceCollection services
    )
    {
        // Регистрируем репозиторий как Singleton
        services.AddSingleton<ICinemaQueueRepository, CinemaQueueRepository>();

        // Регистрируем основной сервис как Singleton
        services.AddSingleton<ICinemaQueueService, CinemaQueueService>();

        // Регистрируем сервис Кинопоиска
        services.AddSingleton<IKinopoiskService, KinopoiskService>();

        // Регистрируем сервис метаданных
        services.AddSingleton<IMediaMetadataService, MediaMetadataService>();

        // Регистрируем сервис уведомлений
        services.AddSingleton<ICinemaQueueNotificationService, CinemaQueueNotificationService>();

        // Регистрируем Twitch интеграцию как BackgroundService
        services.AddHostedService<TwitchCinemaQueueService>();

        // Регистрируем сервис уведомлений как BackgroundService
        services.AddHostedService<CinemaQueueNotificationService>();

        return services;
    }
}
