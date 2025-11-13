namespace MARS.Server.Services.EnvironmentVariable;

/// <summary>
/// Расширения для регистрации сервиса переменных окружения
/// </summary>
public static class EnvironmentVariableServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет сервис переменных окружения в DI контейнер
    /// </summary>
    public static IServiceCollection AddEnvironmentVariableService(this IServiceCollection services)
    {
        // Регистрируем сервис как Singleton, чтобы он мог быть использован в командах и контроллерах
        services.AddSingleton<EnvironmentVariableService>();
        // Регистрируем как HostedService для загрузки переменных при запуске
        services.AddHostedService(sp => sp.GetRequiredService<EnvironmentVariableService>());

        return services;
    }
}
