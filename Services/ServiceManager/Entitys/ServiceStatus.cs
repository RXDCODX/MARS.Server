namespace MARS.Server.Services.ServiceManager.Entitys;

/// <summary>
/// Статус сервиса
/// </summary>
public enum ServiceStatus
{
    Running,
    Stopped,
    Starting,
    Stopping,
    Error,
    Unknown,
}
