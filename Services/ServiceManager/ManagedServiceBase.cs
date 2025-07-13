using MARS.Server.Services.ServiceManager.Entitys;

namespace MARS.Server.Services.ServiceManager;

/// <summary>
/// Базовый класс для управляемых сервисов
/// </summary>
/// <remarks>
/// Конструктор
/// </remarks>
public abstract class ManagedServiceBase(ILogger logger) : IHostedService
{
    /// <summary>
    /// Название сервиса
    /// </summary>
    public abstract string ServiceName { get; }

    /// <summary>
    /// Отображаемое имя сервиса
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Описание сервиса
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Активен ли сервис (обязательная реализация в наследнике)
    /// </summary>
    public abstract bool IsServiceActive { get; set; }

    /// <summary>
    /// Время последней активности
    /// </summary>
    public DateTime? LastActivity { get; protected set; }

    /// <summary>
    /// Время запуска
    /// </summary>
    public DateTime? StartTime { get; protected set; }

    /// <summary>
    /// Статус сервиса
    /// </summary>
    public ServiceStatus Status { get; protected set; } = ServiceStatus.Stopped;

    /// <summary>
    /// Логгер
    /// </summary>
    protected readonly ILogger Logger = logger;

    /// <summary>
    /// Загрузка состояния сервиса из базы данных
    /// </summary>
    public virtual Task LoadStateAsync(ServiceState state)
    {
        IsServiceActive = state.IsServiceActive;
        Status = state.Status;
        // Можно добавить восстановление других параметров при необходимости
        return Task.CompletedTask;
    }

    /// <summary>
    /// Запуск сервиса
    /// </summary>
    public virtual Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Status == ServiceStatus.Running)
        {
            return EvenStarted();
        }
        Status = ServiceStatus.Starting;
        Logger.LogInformation("Starting service {ServiceName}", ServiceName);

        try
        {
            // Логика запуска сервиса (реализуется в наследнике при необходимости)
            Status = ServiceStatus.Running;
            StartTime = DateTime.UtcNow;
            LastActivity = DateTime.UtcNow;
            Logger.LogInformation("Service {ServiceName} started successfully", ServiceName);
            IsServiceActive = true;
        }
        catch (Exception ex)
        {
            Status = ServiceStatus.Error;
            Logger.LogError(ex, "Failed to start service {ServiceName}", ServiceName);
            throw;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Вызывается когда произошла попытка запуска уже запущенного сервиса
    /// </summary>
    /// <returns>Task</returns>
    private static Task EvenStarted()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Остановка сервиса
    /// </summary>
    public virtual Task StopAsync(CancellationToken cancellationToken = default)
    {
        Status = ServiceStatus.Stopping;
        Logger.LogInformation("Stopping service {ServiceName}", ServiceName);

        try
        {
            // Логика остановки сервиса (реализуется в наследнике при необходимости)
            Status = ServiceStatus.Stopped;
            Logger.LogInformation("Service {ServiceName} stopped successfully", ServiceName);
            IsServiceActive = false;
        }
        catch (Exception ex)
        {
            Status = ServiceStatus.Error;
            Logger.LogError(ex, "Failed to stop service {ServiceName}", ServiceName);
            throw;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Обновление времени последней активности
    /// </summary>
    protected void UpdateActivity()
    {
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Получение информации о сервисе
    /// </summary>
    public virtual ServiceInfo GetServiceInfo()
    {
        return new ServiceInfo
        {
            Name = ServiceName,
            DisplayName = DisplayName,
            Description = Description,
            Status = Status,
            StartTime = StartTime,
            LastActivity = LastActivity,
            IsEnabled = IsServiceActive,
            Configuration = GetServiceConfiguration(),
        };
    }

    /// <summary>
    /// Получение конфигурации сервиса (переопределяется в наследниках)
    /// </summary>
    public virtual Dictionary<string, object> GetServiceConfiguration()
    {
        return [];
    }

    /// <summary>
    /// Получение списка доступных команд сервиса (переопределяется в наследниках)
    /// </summary>
    public virtual List<ServiceCommandInfo> GetAvailableCommands()
    {
        return [];
    }

    /// <summary>
    /// Универсальный вызов команды сервиса (переопределяется в наследниках)
    /// </summary>
    public virtual Task<bool> ExecuteCommandAsync(string command)
    {
        return Task.FromResult(false);
    }
}
