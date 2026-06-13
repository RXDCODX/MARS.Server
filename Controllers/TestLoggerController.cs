using System;
using MARS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestLoggerController(ILogger<TestLoggerController> logger) : ControllerBase
{
    [HttpPost("test-warning")]
    public ActionResult<OperationResult> TestWarning()
    {
        logger.LogWarning("Это тестовое предупреждение для проверки логгера в БД");
        ActionResult<OperationResult> result = Ok(OperationResult.Ok("Warning logged"));

        return result;
    }

    [HttpPost("test-error")]
    public ActionResult<OperationResult> TestError()
    {
        try
        {
            throw new InvalidOperationException("Тестовая ошибка для проверки логгера в БД");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Произошла тестовая ошибка");
        }

        ActionResult<OperationResult> result = Ok(OperationResult.Ok("Error logged"));

        return result;
    }

    [HttpPost("test-critical")]
    public ActionResult<OperationResult> TestCritical()
    {
        logger.LogCritical("Это критическая ошибка для проверки логгера в БД");
        ActionResult<OperationResult> result = Ok(OperationResult.Ok("Critical logged"));

        return result;
    }
}
