namespace MARS.Server.Services.KeyboardHook;

public static class KeyboardHookServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет сервис перехвата клавиатуры в коллекцию сервисов
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Коллекция сервисов с добавленным сервисом</returns>
    public static IServiceCollection AddKeyboardHookService(this IServiceCollection services)
    {
        services.AddSingleton<IKeyboardHookService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<IKeyboardHookService>>();
            var hubContext = provider.GetRequiredService<
                IHubContext<TelegramusHub, ITelegramusHub>
            >();
            var lifetime = provider.GetRequiredService<IHostApplicationLifetime>();

            return KeyboardHookFactory.CreateKeyboardHookService(logger, hubContext, lifetime);
        });

        // Регистрируем как HostedService только на Windows
        if (OperatingSystem.IsWindows())
        {
            services.AddHostedService<WindowsKeyboardHookService>(provider =>
                provider.GetRequiredService<IKeyboardHookService>() as WindowsKeyboardHookService
                ?? throw new InvalidOperationException(
                    "Не удалось получить сервис перехвата клавиатуры"
                )
            );
        }

        return services;
    }
}
