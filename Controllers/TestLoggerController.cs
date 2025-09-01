using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestLoggerController(ILogger<TestLoggerController> logger) : ControllerBase
{
    [HttpPost("test-warning")]
    public IActionResult TestWarning()
    {
        logger.LogWarning("Это тестовое предупреждение для проверки логгера в БД");
        return Ok("Warning logged");
    }

    [HttpPost("test-error")]
    public IActionResult TestError()
    {
        try
        {
            throw new InvalidOperationException("Тестовая ошибка для проверки логгера в БД");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Произошла тестовая ошибка");
        }
        return Ok("Error logged");
    }

    [HttpPost("test-critical")]
    public IActionResult TestCritical()
    {
        logger.LogCritical("Это критическая ошибка для проверки логгера в БД");
        return Ok("Critical logged");
    }
}
