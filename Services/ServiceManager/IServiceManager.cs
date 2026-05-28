using System.Collections.Generic;
using MARS.Server.Services.ServiceManager.Entitys;

namespace MARS.Server.Services.ServiceManager;

/// <summary>
/// Интерфейс для управления сервисами приложения
/// </summary>
public interface IServiceManager
{
    /// <summary>
    /// Получить статус всех сервисов
    /// </summary>
    Task<Dictionary<string, ServiceStatus>> GetServicesStatusAsync();

    /// <summary>
    /// Запустить сервис
    /// </summary>
    Task<bool> StartServiceAsync(string serviceName);

    /// <summary>
    /// Остановить сервис
    /// </summary>
    Task<bool> StopServiceAsync(string serviceName);

    /// <summary>
    /// Перезапустить сервис
    /// </summary>
    Task<bool> RestartServiceAsync(string serviceName);

    /// <summary>
    /// Получить информацию о сервисе
    /// </summary>
    Task<ServiceInfo?> GetServiceInfoAsync(string serviceName);

    /// <summary>
    /// Получить логи сервиса
    /// </summary>
    Task<IEnumerable<ServiceLog>> GetServiceLogsAsync(string serviceName, int count = 100);

    /// <summary>
    /// Включить/выключить сервис
    /// </summary>
    Task<bool> SetServiceActiveAsync(string serviceName, bool isActive);

    /// <summary>
    /// Получить все зарегистрированные сервисы
    /// </summary>
    Task<IEnumerable<ServiceInfo>> GetAllServicesAsync();
}
