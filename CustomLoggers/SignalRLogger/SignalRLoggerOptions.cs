namespace MARS.Server.CustomLoggers.SignalRLogger;

public class SignalRLoggerOptions
{
    /// <summary>
    /// Минимальный уровень логирования для отправки через SignalR
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Название источника логов
    /// </summary>
    public string SourceName { get; set; } = "MARS.Server";

    /// <summary>
    /// Категории логов, которые должны быть исключены из отправки
    /// </summary>
    public HashSet<string>? ExcludedCategories { get; set; }

    /// <summary>
    /// Категории логов, которые должны быть включены в отправку (если указаны, то только они)
    /// </summary>
    public HashSet<string>? IncludedCategories { get; set; }

    /// <summary>
    /// Максимальная длина сообщения лога
    /// </summary>
    public int MaxMessageLength { get; set; } = 1000;

    /// <summary>
    /// Включить отправку исключений
    /// </summary>
    public bool IncludeExceptions { get; set; } = true;

    /// <summary>
    /// Включить отправку stack trace
    /// </summary>
    public bool IncludeStackTrace { get; set; } = true;
}
