using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using H.Hooks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.KeyboardHook_UNUSED;

[SupportedOSPlatform("windows")]
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1416 // Validate platform compatibility
#pragma warning restore IDE0079 // Remove unnecessary suppression
public class WindowsKeyboardHookService(
    ILogger logger,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IHostApplicationLifetime lifetime
) : BackgroundService, IKeyboardHookService
{
    private LowLevelKeyboardHook? _keyboardHook;
    private readonly HashSet<Key> _pressedKeys = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public bool IsActive => _keyboardHook is { IsStarted: true };

    // Клавиши для активации Leroy Alert
    private static readonly Key[] RequiredKeys = [Key.A, Key.NumPad9];
    private static readonly Key[] AlternativeKeys = [Key.A, Key.P];

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                logger.LogInformation("Запуск сервиса перехвата клавиатуры");

                // Создаем хук для клавиатуры
                _keyboardHook = new LowLevelKeyboardHook();

                // Подписываемся на события нажатия и отпускания клавиш
                _keyboardHook.Down += OnKeyDown;
                _keyboardHook.Up += OnKeyUp;

                // Запускаем хук
                _keyboardHook.Start();

                logger.LogInformation("Сервис перехвата клавиатуры запущен успешно");
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Сервис перехвата клавиатуры остановлен");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в сервисе перехвата клавиатуры");
            }
            finally
            {
                Cleanup();
            }
        });

        lifetime.ApplicationStopping.Register(() => StopAsync(stoppingToken));

        return Task.CompletedTask;
    }

    [SupportedOSPlatform("windows")]
    private async void OnKeyDown(object? sender, KeyboardEventArgs args)
    {
        await _semaphore.WaitAsync();
        try
        {
            _pressedKeys.Add(args.CurrentKey);

            // Проверяем комбинацию клавиш
            if (CheckKeyCombination())
            {
                logger.LogInformation("Обнаружена комбинация клавиатуры для Leroy Alert");
                _ = TriggerLeroyAlertAsync();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [SupportedOSPlatform("windows")]
    private async void OnKeyUp(object? sender, KeyboardEventArgs args)
    {
        await _semaphore.WaitAsync();
        try
        {
            _pressedKeys.Remove(args.CurrentKey);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [SupportedOSPlatform("windows")]
    private bool CheckKeyCombination()
    {
        return RequiredKeys.All(Func) || AlternativeKeys.All(Predicate);

        bool Predicate(Key key) => _pressedKeys.Contains(key);

        bool Func(Key key) => _pressedKeys.Contains(key);
    }

    private async Task TriggerLeroyAlertAsync()
    {
        try
        {
            logger.LogInformation("Вызов метода LeroyAlert в хабе");
            await hubContext.Clients.All.LeroyAlert();
            logger.LogInformation("Метод LeroyAlert успешно вызван");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при вызове метода LeroyAlert");
        }
    }

    [SupportedOSPlatform("windows")]
    private void Cleanup()
    {
        try
        {
            if (_keyboardHook != null)
            {
                _keyboardHook.Stop();
                _keyboardHook.Down -= OnKeyDown;
                _keyboardHook.Up -= OnKeyUp;
                _keyboardHook.Dispose();
                _keyboardHook = null;
            }

            logger.LogInformation("Сервис перехвата клавиатуры остановлен и очищен");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при очистке сервиса перехвата клавиатуры");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Cleanup();
        return base.StopAsync(cancellationToken);
    }
}
