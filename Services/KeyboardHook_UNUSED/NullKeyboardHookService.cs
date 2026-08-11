namespace MARS.Server.Services.KeyboardHook_UNUSED;

/// <summary>
/// Заглушка для сервиса перехвата клавиатуры на не-Windows платформах
/// </summary>
public class NullKeyboardHookService : IKeyboardHookService
{
    private readonly ILogger _logger;

    public NullKeyboardHookService(ILogger logger)
    {
        _logger = logger;
        _logger.LogInformation(
            "Инициализирована заглушка сервиса перехвата клавиатуры (не-Windows платформа)"
        );
    }

    public bool IsActive => false;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Попытка запуска сервиса перехвата клавиатуры на не-Windows платформе. Операция проигнорирована."
        );
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Попытка остановки сервиса перехвата клавиатуры на не-Windows платформе. Операция проигнорирована."
        );
        return Task.CompletedTask;
    }
}
