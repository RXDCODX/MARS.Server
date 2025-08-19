namespace MARS.Server.DataBaseContext;

/// <summary>
/// Расширения для регистрации DataBaseInitializer
/// </summary>
public static class DataBaseInitializerExtensions
{
    /// <summary>
    /// Добавляет DataBaseInitializer в DI контейнер и регистрирует его как hosted service
    /// </summary>
    public static IServiceCollection AddDataBaseInitializer(this IServiceCollection services)
    {
        services.AddScoped<DataBaseInitializer>();
        services.AddHostedService<DataBaseInitializerHostedService>();

        return services;
    }

    /// <summary>
    /// Регистрирует DataBaseInitializer как hosted service для автоматической инициализации при запуске
    /// </summary>
    public static IHostBuilder UseDataBaseInitializer(this IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices(
            (context, services) =>
            {
                services.AddHostedService<DataBaseInitializerHostedService>();
            }
        );

        return hostBuilder;
    }
}
