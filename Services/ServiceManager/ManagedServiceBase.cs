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
    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsServiceActive)
        {
            Logger.LogWarning(
                "Service {ServiceName} is disabled and cannot be started",
                ServiceName
            );
            return;
        }

        if (Status == ServiceStatus.Running)
        {
            Logger.LogInformation("Service {ServiceName} is already running", ServiceName);
            return;
        }

        Status = ServiceStatus.Starting;
        Logger.LogInformation("Starting service {ServiceName}", ServiceName);

        try
        {
            // Вызываем абстрактный метод, который должен быть реализован в наследнике
            var result = await OnStartAsync(cancellationToken);

            if (result)
            {
                Status = ServiceStatus.Running;
                StartTime = DateTime.UtcNow;
                LastActivity = DateTime.UtcNow;
                Logger.LogInformation("Service {ServiceName} started successfully", ServiceName);
            }
            else
            {
                Status = ServiceStatus.Error;
                Logger.LogError("Service {ServiceName} failed to start", ServiceName);
            }
        }
        catch (Exception ex)
        {
            Status = ServiceStatus.Error;
            Logger.LogError(ex, "Failed to start service {ServiceName}", ServiceName);
            throw;
        }
    }

    /// <summary>
    /// Абстрактный метод для реализации логики запуска в наследниках
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true если сервис успешно запущен, false в противном случае</returns>
    protected abstract Task<bool> OnStartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Абстрактный метод для реализации логики остановки в наследниках
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true если сервис успешно остановлен, false в противном случае</returns>
    protected abstract Task<bool> OnStopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Остановка сервиса
    /// </summary>
    public virtual async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Status == ServiceStatus.Stopped)
        {
            Logger.LogInformation("Service {ServiceName} is already stopped", ServiceName);
            return;
        }

        Status = ServiceStatus.Stopping;
        Logger.LogInformation("Stopping service {ServiceName}", ServiceName);

        try
        {
            // Вызываем абстрактный метод, который должен быть реализован в наследнике
            var result = await OnStopAsync(cancellationToken);

            if (result)
            {
                Status = ServiceStatus.Stopped;
                Logger.LogInformation("Service {ServiceName} stopped successfully", ServiceName);
            }
            else
            {
                Status = ServiceStatus.Error;
                Logger.LogError("Service {ServiceName} failed to stop", ServiceName);
            }
        }
        catch (Exception ex)
        {
            Status = ServiceStatus.Error;
            Logger.LogError(ex, "Failed to stop service {ServiceName}", ServiceName);
            throw;
        }
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
        };
    }
}
