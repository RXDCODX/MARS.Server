using System;

namespace MARS.Server.Services.Shikimori.Entitys;

/// <summary>
/// Информация о состоянии рейт лимитера
/// </summary>
public record RateLimiterInfo
{
    /// <summary>
    /// Доступные запросы в секунду
    /// </summary>
    public int AvailablePerSecond { get; init; }

    /// <summary>
    /// Доступные запросы в минуту
    /// </summary>
    public int AvailablePerMinute { get; init; }

    /// <summary>
    /// Время до сброса секундного лимита
    /// </summary>
    public TimeSpan TimeToResetSecond { get; init; }

    /// <summary>
    /// Время до сброса минутного лимита
    /// </summary>
    public TimeSpan TimeToResetMinute { get; init; }
}
