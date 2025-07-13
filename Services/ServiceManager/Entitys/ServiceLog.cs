namespace MARS.Server.Services.ServiceManager.Entitys;

/// <summary>
/// Лог сервиса
/// </summary>
public class ServiceLog
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}
