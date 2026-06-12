using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.ChannelPoints;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

/// <summary>
/// Сервис кеширования наград канала для оптимизации частых запросов.
/// Снижает нагрузку на Twitch API, уменьшая количество запросов.
/// </summary>
public interface IRewardsCacheService
{
    /// <summary>
    /// Получить все награды канала с кешированием.
    /// </summary>
    /// <returns>Список наград или null при ошибке</returns>
    Task<IEnumerable<CustomReward>?> GetRewardsAsync();

    /// <summary>
    /// Инвалидировать кеш наград (например, после создания/обновления/удаления награды).
    /// </summary>
    Task InvalidateCacheAsync();

    /// <summary>
    /// Получить информацию о кеше для отладки.
    /// </summary>
    CacheInfo GetCacheInfo();
}

/// <summary>
/// Информация о состоянии кеша.
/// </summary>
public record CacheInfo(
    bool IsCached,
    DateTime? CachedAt,
    int? CachedRewardsCount,
    TimeSpan? TimeToExpire
);
