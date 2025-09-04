using MARS.Server.CustomLoggers.DatabaseLogger;
using MARS.Server.Services.Logs.Interfaces;

namespace MARS.Server.Services.Logs.Services;

public class LogsService(LoggerDbContext dbContext) : ILogsService
{
    public async Task<(IEnumerable<Log> Logs, int TotalCount)> GetLogsAsync(
        int page = 1,
        int pageSize = 50,
        string? sortBy = null,
        bool sortDescending = true,
        LogLevel? logLevel = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchText = null
    )
    {
        var query = dbContext.Logs.AsNoTracking();

        // Фильтры
        if (logLevel != null)
        {
            query = query.Where(l => l.LogLevel == logLevel);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(l => l.WhenLogged >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(l => l.WhenLogged <= toDate.Value);
        }

        if (!string.IsNullOrEmpty(searchText))
        {
            query = query.Where(l =>
                l.Message.Contains(searchText)
                || (l.StackTrace != null && l.StackTrace.Contains(searchText))
            );
        }

        // Получаем общее количество для пагинации
        var totalCount = await query.CountAsync();

        // Сортировка
        query = sortBy?.ToLower() switch
        {
            "whenlogged" => sortDescending
                ? query.OrderByDescending(l => l.WhenLogged)
                : query.OrderBy(l => l.WhenLogged),
            "loglevel" => sortDescending
                ? query.OrderByDescending(l => l.LogLevel)
                : query.OrderBy(l => l.LogLevel),
            "message" => sortDescending
                ? query.OrderByDescending(l => l.Message)
                : query.OrderBy(l => l.Message),
            _ => query.OrderByDescending(l => l.WhenLogged), // По умолчанию сортируем по дате
        };

        // Пагинация
        var logs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return (logs, totalCount);
    }

    public async Task<IEnumerable<Log>> GetLogsByLevelAsync(LogLevel logLevel)
    {
        return await dbContext
            .Logs.AsNoTracking()
            .Where(l => l.LogLevel == logLevel)
            .OrderByDescending(l => l.WhenLogged)
            .ToListAsync();
    }

    public async Task<IEnumerable<Log>> GetLogsByDateRangeAsync(DateTime fromDate, DateTime toDate)
    {
        return await dbContext
            .Logs.AsNoTracking()
            .Where(l => l.WhenLogged >= fromDate && l.WhenLogged <= toDate)
            .OrderByDescending(l => l.WhenLogged)
            .ToListAsync();
    }

    public async Task<IEnumerable<Log>> GetRecentLogsAsync(int count = 100)
    {
        return await dbContext
            .Logs.AsNoTracking()
            .OrderByDescending(l => l.WhenLogged)
            .Take(count)
            .ToListAsync();
    }

    public async Task<LogsStatistics> GetLogsStatisticsAsync()
    {
        var stats = new LogsStatistics { TotalLogs = await dbContext.Logs.CountAsync() };

        // Получаем статистику по уровням логирования
        var levelStats = await dbContext
            .Logs.AsNoTracking()
            .GroupBy(l => l.LogLevel)
            .Select(g => new { LogLevel = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var stat in levelStats)
        {
            switch (stat.LogLevel)
            {
                case LogLevel.Warning:
                    stats.WarningLogs = stat.Count;
                    break;
                case LogLevel.Error:
                    stats.ErrorLogs = stat.Count;
                    break;
                case LogLevel.Critical:
                    stats.CriticalLogs = stat.Count;
                    break;
            }
        }

        // Получаем даты
        var oldestLog = await dbContext
            .Logs.AsNoTracking()
            .OrderBy(l => l.WhenLogged)
            .Select(l => l.WhenLogged)
            .FirstOrDefaultAsync();

        var newestLog = await dbContext
            .Logs.AsNoTracking()
            .OrderByDescending(l => l.WhenLogged)
            .Select(l => l.WhenLogged)
            .FirstOrDefaultAsync();

        stats.OldestLogDate = oldestLog != default ? oldestLog : null;
        stats.NewestLogDate = newestLog != default ? newestLog : null;

        return stats;
    }
}
