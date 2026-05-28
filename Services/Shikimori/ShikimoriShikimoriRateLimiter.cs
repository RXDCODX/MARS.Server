using System.Linq;
using System.Threading;
using MARS.Server.Services.Shikimori.Entitys;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Shikimori;

/// <summary>
/// Реализация рейт лимитера для Shikimori API с ограничениями 5rps и 90rpm
/// </summary>
public class ShikimoriShikimoriRateLimiter(ILogger<ShikimoriShikimoriRateLimiter> logger)
    : IShikimoriRateLimiter
{
    private readonly ILogger<ShikimoriShikimoriRateLimiter> _logger = logger;
    private readonly SemaphoreSlim _semaphore = new(MaxConcurrentRequests, MaxConcurrentRequests);
    private readonly ConcurrentQueue<DateTime> _requestsPerSecond = new();
    private readonly ConcurrentQueue<DateTime> _requestsPerMinute = new();
    private readonly object _lockObject = new();

    private const int MaxRequestsPerSecond = 5;
    private const int MaxRequestsPerMinute = 90;
    private const int MaxConcurrentRequests = 10; // Максимальное количество одновременных запросов

    public async Task<bool> TryAcquireAsync()
    {
        var result = false;

        if (await _semaphore.WaitAsync(0)) // Пытаемся получить слот без ожидания
        {
            try
            {
                if (CanMakeRequest())
                {
                    RecordRequest();
                    result = true;
                }
                else
                {
                    _semaphore.Release();
                }
            }
            catch
            {
                _semaphore.Release();
                throw;
            }
        }

        return result;
    }

    public async Task WaitForSlotAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            while (!CanMakeRequest())
            {
                var delay = CalculateDelay();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }

            RecordRequest();
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    public RateLimiterInfo GetInfo()
    {
        var now = DateTime.UtcNow;
        var availablePerSecond = CalculateAvailablePerSecond(now);
        var availablePerMinute = CalculateAvailablePerMinute(now);
        var timeToResetSecond = CalculateTimeToResetSecond(now);
        var timeToResetMinute = CalculateTimeToResetMinute(now);

        return new RateLimiterInfo
        {
            AvailablePerSecond = availablePerSecond,
            AvailablePerMinute = availablePerMinute,
            TimeToResetSecond = timeToResetSecond,
            TimeToResetMinute = timeToResetMinute,
        };
    }

    private bool CanMakeRequest()
    {
        var now = DateTime.UtcNow;
        CleanupOldRequests(now);

        var canMakeSecond = _requestsPerSecond.Count < MaxRequestsPerSecond;
        var canMakeMinute = _requestsPerMinute.Count < MaxRequestsPerMinute;

        return canMakeSecond && canMakeMinute;
    }

    private void RecordRequest()
    {
        var now = DateTime.UtcNow;
        _requestsPerSecond.Enqueue(now);
        _requestsPerMinute.Enqueue(now);
    }

    private void CleanupOldRequests(DateTime now)
    {
        // Очищаем запросы старше 1 секунды
        while (
            _requestsPerSecond.TryPeek(out var requestTime)
            && now - requestTime > TimeSpan.FromSeconds(1)
        )
        {
            _requestsPerSecond.TryDequeue(out _);
        }

        // Очищаем запросы старше 1 минуты
        while (
            _requestsPerMinute.TryPeek(out var requestTime)
            && now - requestTime > TimeSpan.FromMinutes(1)
        )
        {
            _requestsPerMinute.TryDequeue(out _);
        }
    }

    private int CalculateAvailablePerSecond(DateTime now)
    {
        CleanupOldRequests(now);
        return Math.Max(0, MaxRequestsPerSecond - _requestsPerSecond.Count);
    }

    private int CalculateAvailablePerMinute(DateTime now)
    {
        CleanupOldRequests(now);
        return Math.Max(0, MaxRequestsPerMinute - _requestsPerMinute.Count);
    }

    private TimeSpan CalculateTimeToResetSecond(DateTime now)
    {
        if (_requestsPerSecond.IsEmpty)
        {
            return TimeSpan.Zero;
        }

        var oldestRequest = _requestsPerSecond.Min();
        var resetTime = oldestRequest.AddSeconds(1);
        return resetTime > now ? resetTime - now : TimeSpan.Zero;
    }

    private TimeSpan CalculateTimeToResetMinute(DateTime now)
    {
        if (_requestsPerMinute.IsEmpty)
        {
            return TimeSpan.Zero;
        }

        var oldestRequest = _requestsPerMinute.Min();
        var resetTime = oldestRequest.AddMinutes(1);
        return resetTime > now ? resetTime - now : TimeSpan.Zero;
    }

    private TimeSpan CalculateDelay()
    {
        var now = DateTime.UtcNow;
        var timeToResetSecond = CalculateTimeToResetSecond(now);
        var timeToResetMinute = CalculateTimeToResetMinute(now);

        // Возвращаем минимальное время ожидания
        return timeToResetSecond < timeToResetMinute ? timeToResetSecond : timeToResetMinute;
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}
