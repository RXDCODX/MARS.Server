using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoggerTestController(ILogger<LoggerTestController> logger) : ControllerBase
{
    /// <summary>
    /// Тестирует различные уровни логирования через SignalR
    /// </summary>
    [HttpPost("test-logging")]
    public ActionResult<object> TestLogging()
    {
        ActionResult<object> result = Ok(new { message = "Тестовые логи отправлены через SignalR" });
        
        logger.LogTrace("Это сообщение уровня Trace - детальная отладочная информация");
        logger.LogDebug("Это сообщение уровня Debug - отладочная информация");
        logger.LogInformation("Это сообщение уровня Information - общая информация");
        logger.LogWarning("Это сообщение уровня Warning - предупреждение");
        logger.LogError("Это сообщение уровня Error - ошибка");
        logger.LogCritical("Это сообщение уровня Critical - критическая ошибка");
        
        return result;
    }

    /// <summary>
    /// Тестирует логирование с исключением
    /// </summary>
    [HttpPost("test-exception")]
    public ActionResult<object> TestException()
    {
        ActionResult<object> result = Ok(new { message = "Тестовое исключение залогировано через SignalR" });
        
        try
        {
            throw new InvalidOperationException("Это тестовое исключение для демонстрации логирования");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Произошла ошибка при выполнении тестового метода");
        }
        
        return result;
    }

    /// <summary>
    /// Тестирует структурированное логирование
    /// </summary>
    [HttpPost("test-structured")]
    public ActionResult<object> TestStructuredLogging()
    {
        ActionResult<object> result = Ok(new { message = "Структурированные логи отправлены через SignalR" });
        
        var userId = "user123";
        var action = "test_action";
        var duration = 150;

        logger.LogInformation(
            "Пользователь {UserId} выполнил действие {Action} за {Duration}ms",
            userId, action, duration);

        logger.LogWarning(
            "Попытка доступа пользователя {UserId} к ресурсу {Resource} была отклонена",
            userId, "protected_resource");
        
        return result;
    }
}
