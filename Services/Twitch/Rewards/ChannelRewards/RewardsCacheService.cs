using System.Collections.Immutable;
using TwitchLib.Api.Helix.Models.ChannelPoints;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

/// <summary>
/// Реализация кеширования наград канала.
/// Кеширует результаты запросов на 2 минуты для снижения нагрузки на Twitch API.
/// </summary>
public class RewardsCacheService(ChannelRewardsService channelRewardsService, ILogger logger)
    : IRewardsCacheService
{
    private readonly SemaphoreSlim _semaphore = new(1);
    private ImmutableList<CustomReward>? _cachedRewards;
    private DateTime? _cacheExpirationTime;
    private const int CacheTtlMinutes = 2;

    /// <summary>
    /// Получить все награды канала с кешированием.
    /// </summary>
    public async Task<IEnumerable<CustomReward>?> GetRewardsAsync()
    {
        await _semaphore.WaitAsync();

        if (IsCacheValid())
        {
            {
                if (IsCacheValid() && _cachedRewards != null)
                {
                    logger.LogDebug(
                        "Использование кешированных наград ({Count} шт)",
                        _cachedRewards.Count
                    );
                    _semaphore.Release();
                    return _cachedRewards.AsEnumerable();
                }
            }
            _semaphore.Release();
        }

        try
        {
            var rewards = await channelRewardsService.GetRewardsAsync();
            rewards = rewards?.ToArray();
            if (rewards is not null)
            {
                _cachedRewards = rewards.ToImmutableList();
                _cacheExpirationTime = DateTime.UtcNow.AddMinutes(CacheTtlMinutes);

                logger.LogInformation(
                    "Кеш наград обновлён ({Count} шт, TTL: {Minutes} мин)",
                    _cachedRewards.Count,
                    CacheTtlMinutes
                );
                _semaphore.Release();

                return rewards;
            }
            else
            {
                logger.LogWarning("GetRewardsAsync вернул null");
                _semaphore.Release();

                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Исключение при получении наград");
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Инвалидировать кеш наград.
    /// </summary>
    public async Task InvalidateCacheAsync()
    {
        await _semaphore.WaitAsync();
        _cachedRewards = null;
        _cacheExpirationTime = null;
        logger.LogInformation("Кеш наград инвалидирован");
        _semaphore.Release();
    }

    /// <summary>
    /// Получить информацию о кеше для отладки.
    /// </summary>
    public CacheInfo GetCacheInfo()
    {
        _semaphore.Wait();

        var isCached = IsCacheValid();
        _semaphore.Release();

        return new CacheInfo(
            IsCached: isCached,
            CachedAt: _cacheExpirationTime?.AddMinutes(-CacheTtlMinutes),
            CachedRewardsCount: _cachedRewards?.Count,
            TimeToExpire: _cacheExpirationTime.HasValue
                ? _cacheExpirationTime.Value - DateTime.UtcNow
                : null
        );
    }

    private bool IsCacheValid()
    {
        return _cachedRewards != null
            && _cacheExpirationTime.HasValue
            && DateTime.UtcNow < _cacheExpirationTime.Value;
    }
}
