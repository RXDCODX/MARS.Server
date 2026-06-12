using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MARS.Server.CustomLoggers.DatabaseLogger;

namespace MARS.Server.Services.Logs.Interfaces;

public interface ILogsService
{
    /// <summary>
    /// Получить все логи с пагинацией и сортировкой
    /// </summary>
    Task<(IEnumerable<Log> Logs, int TotalCount)> GetLogsAsync(
        int page = 1,
        int pageSize = 50,
        string? sortBy = null,
        bool sortDescending = true,
        LogLevel? logLevel = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchText = null
    );

    /// <summary>
    /// Получить логи по уровню логирования
    /// </summary>
    Task<IEnumerable<Log>> GetLogsByLevelAsync(LogLevel logLevel);

    /// <summary>
    /// Получить логи за указанный период
    /// </summary>
    Task<IEnumerable<Log>> GetLogsByDateRangeAsync(DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Получить последние логи
    /// </summary>
    Task<IEnumerable<Log>> GetRecentLogsAsync(int count = 100);

    /// <summary>
    /// Получить статистику по логам
    /// </summary>
    Task<LogsStatistics> GetLogsStatisticsAsync();
}

/// <summary>
/// Статистика по логам
/// </summary>
public class LogsStatistics
{
    public int TotalLogs { get; set; }
    public int WarningLogs { get; set; }
    public int ErrorLogs { get; set; }
    public int CriticalLogs { get; set; }
    public DateTime? OldestLogDate { get; set; }
    public DateTime? NewestLogDate { get; set; }
}
