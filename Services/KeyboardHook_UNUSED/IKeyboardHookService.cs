namespace MARS.Server.Services.KeyboardHook;

public interface IKeyboardHookService
{
    /// <summary>
    /// Запускает мониторинг клавиатуры
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Останавливает мониторинг клавиатуры
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет, активен ли сервис
    /// </summary>
    bool IsActive { get; }
}
