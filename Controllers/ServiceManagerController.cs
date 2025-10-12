using MARS.Server.Services;
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
    public async Task<
        ActionResult<OperationResult<Dictionary<string, ServiceStatus>>>
    > GetServicesStatus()
    {
        ActionResult<OperationResult<Dictionary<string, ServiceStatus>>> result;
        try
        {
            var statuses = await serviceManager.GetServicesStatusAsync();
            result = Ok(
                OperationResult<Dictionary<string, ServiceStatus>>.Ok(
                    "Получены статусы сервисов",
                    statuses
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get services status");
            result = Ok(
                OperationResult<Dictionary<string, ServiceStatus>>.Bad(
                    "Ошибка при получении статусов сервисов",
                    []
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить информацию о конкретном сервисе
    /// </summary>
    [HttpGet("service/{serviceName}")]
    public async Task<ActionResult<OperationResult<ServiceInfo?>>> GetServiceInfo(
        string serviceName
    )
    {
        ActionResult<OperationResult<ServiceInfo?>> result;
        try
        {
            var serviceInfo = await serviceManager.GetServiceInfoAsync(serviceName);

            if (serviceInfo != null)
            {
                result = Ok(
                    OperationResult<ServiceInfo?>.Ok("Получена информация о сервисе", serviceInfo)
                );
            }
            else
            {
                result = Ok(
                    OperationResult<ServiceInfo?>.Bad($"Service '{serviceName}' not found", null)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get service info for {ServiceName}", serviceName);
            result = Ok(
                OperationResult<ServiceInfo?>.Bad("Ошибка при получении информации о сервисе", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Запустить сервис
    /// </summary>
    [HttpPost("service/{serviceName}/start")]
    public async Task<ActionResult<OperationResult>> StartService(string serviceName)
    {
        ActionResult<OperationResult> result;
        try
        {
            var success = await serviceManager.StartServiceAsync(serviceName);

            if (success)
            {
                result = Ok(OperationResult.Ok($"Service '{serviceName}' started successfully"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"Failed to start service '{serviceName}'"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start service {ServiceName}", serviceName);
            result = Ok(OperationResult.Bad("Ошибка при запуске сервиса"));
        }

        return result;
    }

    /// <summary>
    /// Остановить сервис
    /// </summary>
    [HttpPost("service/{serviceName}/stop")]
    public async Task<ActionResult<OperationResult>> StopService(string serviceName)
    {
        ActionResult<OperationResult> result;
        try
        {
            var success = await serviceManager.StopServiceAsync(serviceName);

            if (success)
            {
                result = Ok(OperationResult.Ok($"Service '{serviceName}' stopped successfully"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"Failed to stop service '{serviceName}'"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop service {ServiceName}", serviceName);
            result = Ok(OperationResult.Bad("Ошибка при остановке сервиса"));
        }

        return result;
    }

    /// <summary>
    /// Перезапустить сервис
    /// </summary>
    [HttpPost("service/{serviceName}/restart")]
    public async Task<ActionResult<OperationResult>> RestartService(string serviceName)
    {
        ActionResult<OperationResult> result;
        try
        {
            var success = await serviceManager.RestartServiceAsync(serviceName);

            if (success)
            {
                result = Ok(OperationResult.Ok($"Service '{serviceName}' restarted successfully"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"Failed to restart service '{serviceName}'"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart service {ServiceName}", serviceName);
            result = Ok(OperationResult.Bad("Ошибка при перезапуске сервиса"));
        }

        return result;
    }

    /// <summary>
    /// Получить логи сервиса
    /// </summary>
    [HttpGet("service/{serviceName}/logs")]
    public async Task<ActionResult<OperationResult<IEnumerable<ServiceLog>>>> GetServiceLogs(
        string serviceName,
        [FromQuery] int count = 100
    )
    {
        ActionResult<OperationResult<IEnumerable<ServiceLog>>> result;
        try
        {
            var logs = await serviceManager.GetServiceLogsAsync(serviceName, count);
            result = Ok(OperationResult<IEnumerable<ServiceLog>>.Ok("Получены логи сервиса", logs));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get logs for service {ServiceName}", serviceName);
            result = Ok(
                OperationResult<IEnumerable<ServiceLog>>.Bad(
                    "Ошибка при получении логов сервиса",
                    []
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Включить/выключить сервис
    /// </summary>
    [HttpPost("service/{serviceName}/active")]
    public async Task<ActionResult<OperationResult>> SetServiceActive(
        string serviceName,
        [FromBody] bool isActive
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            var success = await serviceManager.SetServiceActiveAsync(serviceName, isActive);

            if (success)
            {
                result = Ok(
                    OperationResult.Ok($"Service '{serviceName}' active state set to {isActive}")
                );
            }
            else
            {
                result = Ok(
                    OperationResult.Bad($"Failed to set service '{serviceName}' active state")
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set service {ServiceName} active state", serviceName);
            result = Ok(OperationResult.Bad("Ошибка при изменении активности сервиса"));
        }

        return result;
    }

    /// <summary>
    /// Получить все сервисы
    /// </summary>
    [HttpGet("services")]
    public async Task<ActionResult<OperationResult<IEnumerable<ServiceInfo>>>> GetAllServices()
    {
        ActionResult<OperationResult<IEnumerable<ServiceInfo>>> result;
        try
        {
            var services = await serviceManager.GetAllServicesAsync();
            result = Ok(
                OperationResult<IEnumerable<ServiceInfo>>.Ok("Получены все сервисы", services)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all services");
            result = Ok(
                OperationResult<IEnumerable<ServiceInfo>>.Bad(
                    "Ошибка при получении всех сервисов",
                    []
                )
            );
        }

        return result;
    }
}
