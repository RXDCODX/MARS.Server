using MARS.Server.Services.CommandExecutor.Adapters;

namespace MARS.Server.Services.CommandExecutor;

/// <summary>
/// Расширения для регистрации сервисов команд
/// </summary>
public static class CommandExecutorServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет все сервисы команд в DI контейнер
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Коллекция сервисов</returns>
    public static IServiceCollection AddCommandExecutorServices(this IServiceCollection services)
    {
        // Регистрируем фабрику команд
        services.AddSingleton<CommandFactory>();

        // Регистрируем платформенные сервисы
        services.AddSingleton<TelegramCommandService>();
        services.AddSingleton<TwitchCommandService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchCommandService>());

        // Регистрируем CommandExecutorService как Scoped
        services.AddSingleton<CommandExecutorService>();
        services.AddSingleton<ICommandService>(sp =>
            sp.GetRequiredService<CommandExecutorService>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<CommandExecutorService>());

        // Регистрируем API адаптер
        services.AddSingleton<ApiCommandService>();

        return services;
    }
}
