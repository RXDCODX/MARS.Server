using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestLoggerController(ILogger<TestLoggerController> logger) : ControllerBase
{
    [HttpPost("test-warning")]
    public ActionResult<string> TestWarning()
    {
        ActionResult<string> result = Ok("Warning logged");
        
        logger.LogWarning("Это тестовое предупреждение для проверки логгера в БД");
        
        return result;
    }

    [HttpPost("test-error")]
    public ActionResult<string> TestError()
    {
        ActionResult<string> result = Ok("Error logged");
        
        try
        {
            throw new InvalidOperationException("Тестовая ошибка для проверки логгера в БД");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Произошла тестовая ошибка");
        }
        
        return result;
    }

    [HttpPost("test-critical")]
    public ActionResult<string> TestCritical()
    {
        ActionResult<string> result = Ok("Critical logged");
        
        logger.LogCritical("Это критическая ошибка для проверки логгера в БД");
        
        return result;
    }
}
