using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.ServiceManager.Entitys;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceManagerController(
    IServiceManager serviceManager,
    ILogger<ServiceManagerController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить статус всех сервисов
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<Dictionary<string, ServiceStatus>>> GetServicesStatus()
    {
        try
        {
            var statuses = await serviceManager.GetServicesStatusAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get services status");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Получить информацию о конкретном сервисе
    /// </summary>
    [HttpGet("service/{serviceName}")]
    public async Task<ActionResult<ServiceInfo>> GetServiceInfo(string serviceName)
    {
        try
        {
            var serviceInfo = await serviceManager.GetServiceInfoAsync(serviceName);
            return serviceInfo == null
                ? NotFound($"Service '{serviceName}' not found")
                : Ok(serviceInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get service info for {ServiceName}", serviceName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Запустить сервис
    /// </summary>
    [HttpPost("service/{serviceName}/start")]
    public async Task<ActionResult> StartService(string serviceName)
    {
        try
        {
            var success = await serviceManager.StartServiceAsync(serviceName);
            return !success
                ? BadRequest($"Failed to start service '{serviceName}'")
                : Ok(new { message = $"Service '{serviceName}' started successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start service {ServiceName}", serviceName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Остановить сервис
    /// </summary>
    [HttpPost("service/{serviceName}/stop")]
    public async Task<ActionResult> StopService(string serviceName)
    {
        try
        {
            var success = await serviceManager.StopServiceAsync(serviceName);
            return !success
                ? BadRequest($"Failed to stop service '{serviceName}'")
                : Ok(new { message = $"Service '{serviceName}' stopped successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop service {ServiceName}", serviceName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Перезапустить сервис
    /// </summary>
    [HttpPost("service/{serviceName}/restart")]
    public async Task<ActionResult> RestartService(string serviceName)
    {
        try
        {
            var success = await serviceManager.RestartServiceAsync(serviceName);
            return !success
                ? BadRequest($"Failed to restart service '{serviceName}'")
                : Ok(new { message = $"Service '{serviceName}' restarted successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart service {ServiceName}", serviceName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Получить логи сервиса
    /// </summary>
    [HttpGet("service/{serviceName}/logs")]
    public async Task<ActionResult<IEnumerable<ServiceLog>>> GetServiceLogs(
        string serviceName,
        [FromQuery] int count = 100
    )
    {
        try
        {
            var logs = await serviceManager.GetServiceLogsAsync(serviceName, count);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get logs for service {ServiceName}", serviceName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Включить/выключить сервис
    /// </summary>
    [HttpPost("service/{serviceName}/active")]
    public async Task<ActionResult> SetServiceActive(string serviceName, [FromBody] bool isActive)
    {
        try
        {
            var success = await serviceManager.SetServiceActiveAsync(serviceName, isActive);
            return !success
                ? BadRequest($"Failed to set service '{serviceName}' active state")
                : Ok(new { message = $"Service '{serviceName}' active state set to {isActive}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set service {ServiceName} active state", serviceName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Получить все сервисы
    /// </summary>
    [HttpGet("services")]
    public async Task<ActionResult<IEnumerable<ServiceInfo>>> GetAllServices()
    {
        try
        {
            var services = await serviceManager.GetAllServicesAsync();
            return Ok(services);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all services");
            return StatusCode(500, "Internal server error");
        }
    }
}
