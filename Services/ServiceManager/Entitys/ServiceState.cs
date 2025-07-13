namespace MARS.Server.Services.ServiceManager.Entitys;

/// <summary>
/// Состояние сервиса в базе данных
/// </summary>
public class ServiceState
{
    /// <summary>
    /// Идентификатор
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Название сервиса
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Отображаемое имя сервиса
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Описание сервиса
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Активен ли сервис
    /// </summary>
    public bool IsServiceActive { get; set; } = true;

    /// <summary>
    /// Статус сервиса
    /// </summary>
    public ServiceStatus Status { get; set; } = ServiceStatus.Stopped;

    /// <summary>
    /// Время последнего запуска
    /// </summary>
    public DateTime? LastStartTime { get; set; }

    /// <summary>
    /// Время последней активности
    /// </summary>
    public DateTime? LastActivity { get; set; }

    /// <summary>
    /// Время создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Конфигурация сервиса в JSON формате
    /// </summary>
    public string? ConfigurationJson { get; set; }
}
