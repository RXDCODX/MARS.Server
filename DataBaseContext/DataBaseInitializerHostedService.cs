namespace MARS.Server.DataBaseContext;

/// <summary>
/// Hosted service для автоматической инициализации базы данных при запуске приложения
/// </summary>
public class DataBaseInitializerHostedService(
    IServiceProvider serviceProvider,
    ILogger<DataBaseInitializerHostedService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Запуск инициализации базы данных...");

            // Ждем немного, чтобы все сервисы успели инициализироваться
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            using var scope = serviceProvider.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<DataBaseInitializer>();

            await initializer.InitializeAsync();

            logger.LogInformation("Инициализация базы данных завершена успешно");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при инициализации базы данных");
            // Не прерываем работу приложения, если инициализация не удалась
        }
    }
}
