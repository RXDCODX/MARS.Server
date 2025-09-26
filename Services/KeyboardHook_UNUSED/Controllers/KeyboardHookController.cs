using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Services.KeyboardHook.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KeyboardHookController(
    IKeyboardHookService keyboardHookService,
    ILogger<KeyboardHookController> logger
) : ControllerBase
{
    /// <summary>
    /// Получает статус сервиса перехвата клавиатуры
    /// </summary>
    /// <returns>Статус сервиса</returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var status = new
            {
                keyboardHookService.IsActive,
                Platform = OperatingSystem.IsWindows() ? "Windows" : "Non-Windows",
                Timestamp = DateTimeOffset.UtcNow,
                ServiceName = "Keyboard Hook Service",
                Description = OperatingSystem.IsWindows()
                    ? "Сервис для перехвата комбинаций клавиш A+Numpad9 или A+P"
                    : "Заглушка сервиса (не-Windows платформа)",
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статуса сервиса перехвата клавиатуры");
            return StatusCode(500, new { Error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получает информацию о поддерживаемых комбинациях клавиш
    /// </summary>
    /// <returns>Список поддерживаемых комбинаций</returns>
    [HttpGet("combinations")]
    public IActionResult GetSupportedCombinations()
    {
        var combinations = new[]
        {
            new
            {
                Name = "Leroy Alert - Primary",
                Keys = new[] { "A", "Numpad 9" },
                Description = "Основная комбинация для активации Leroy Alert",
            },
            new
            {
                Name = "Leroy Alert - Alternative",
                Keys = new[] { "A", "P" },
                Description = "Альтернативная комбинация для активации Leroy Alert",
            },
        };

        return Ok(
            new
            {
                Combinations = combinations,
                TotalCount = combinations.Length,
                Timestamp = DateTimeOffset.UtcNow,
            }
        );
    }

    /// <summary>
    /// Получает детальную информацию о сервисе
    /// </summary>
    /// <returns>Детальная информация о сервисе</returns>
    [HttpGet("info")]
    public IActionResult GetServiceInfo()
    {
        var isWindows = OperatingSystem.IsWindows();
        var info = new
        {
            ServiceName = "Keyboard Hook Service",
            Version = "1.0.0",
            Platform = isWindows ? "Windows" : "Non-Windows",
            Description = isWindows
                ? "Сервис для глобального перехвата клавиатуры и активации SignalR методов"
                : "Заглушка сервиса для не-Windows платформ",
            Features = isWindows
                ?
                [
                    "Глобальный перехват клавиатуры",
                    "Отслеживание комбинаций клавиш",
                    "Автоматический вызов SignalR методов",
                    "Логирование всех событий",
                    "Безопасная остановка и очистка ресурсов",
                ]
                : new[]
                {
                    "Заглушка для не-Windows платформ",
                    "Логирование попыток использования",
                    "Совместимость с существующим кодом",
                },
            Requirements = isWindows
                ?
                [
                    "Windows OS",
                    "Права администратора (для глобального перехвата)",
                    "Библиотека H.Hooks",
                ]
                : new[] { "Любая ОС (кроме Windows)", "Совместимость с .NET" },
            Status = new { keyboardHookService.IsActive, LastUpdate = DateTimeOffset.UtcNow },
        };

        return Ok(info);
    }
}
