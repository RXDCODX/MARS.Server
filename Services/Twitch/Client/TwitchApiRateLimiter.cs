using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Core.Interfaces;

namespace MARS.Server.Services.Twitch.Client;

/// <summary>
/// Рейт лимитер для Twitch API с ограничениями:
/// - 800 запросов в минуту
/// - 1 запрос в 2 секунды
/// </summary>
[DebuggerNonUserCode]
public class TwitchApiRateLimiter(ILogger<TwitchApiRateLimiter> logger) : IRateLimiter, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly SemaphoreSlim _queueSemaphore = new(1, 1);
    private readonly Queue<DateTime> _requestTimes = new();

    private const int MaxRequestsPerMinute = 800;
    private const int MinIntervalBetweenRequestsMs = 1500; // 1.5 секунды

    public async Task Perform(Func<Task> perform, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await WaitForRateLimit();
            var startTime = DateTime.UtcNow;

            try
            {
                await perform();
                await RecordRequest(startTime);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при выполнении запроса к Twitch API");
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task Perform(Func<Task> perform)
    {
        await Perform(perform, CancellationToken.None);
    }

    public async Task<T> Perform<T>(Func<Task<T>> perform, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await WaitForRateLimit();
            var startTime = DateTime.UtcNow;

            try
            {
                var result = await perform();
                await RecordRequest(startTime);
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при выполнении запроса к Twitch API");
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T> Perform<T>(Func<Task<T>> perform)
    {
        return await Perform(perform, CancellationToken.None);
    }

    public async Task Perform(Action perform, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await WaitForRateLimit();
            var startTime = DateTime.UtcNow;

            try
            {
                perform();
                await RecordRequest(startTime);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при выполнении запроса к Twitch API");
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task Perform(Action perform)
    {
        await Perform(perform, CancellationToken.None);
    }

    public async Task<T> Perform<T>(Func<T> perform, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await WaitForRateLimit();
            var startTime = DateTime.UtcNow;

            try
            {
                var result = perform();
                await RecordRequest(startTime);
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при выполнении запроса к Twitch API");
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T> Perform<T>(Func<T> perform)
    {
        return await Perform(perform, CancellationToken.None);
    }

    private async Task WaitForRateLimit()
    {
        TimeSpan? waitTime = null;

        await _queueSemaphore.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;

            // Удаляем старые записи (старше минуты)
            while (_requestTimes.Count > 0 && now - _requestTimes.Peek() > TimeSpan.FromMinutes(1))
            {
                _requestTimes.Dequeue();
            }

            // Проверяем лимит запросов в минуту
            if (_requestTimes.Count >= MaxRequestsPerMinute)
            {
                var oldestRequest = _requestTimes.Peek();
                var minuteWaitTime = TimeSpan.FromMinutes(1) - (now - oldestRequest);
                if (minuteWaitTime > TimeSpan.Zero)
                {
                    logger.LogWarning(
                        "Достигнут лимит запросов в минуту ({MaxRequests}). Ожидание {WaitTime}ms",
                        MaxRequestsPerMinute,
                        minuteWaitTime.TotalMilliseconds
                    );

                    waitTime = minuteWaitTime;
                }
            }

            // Проверяем минимальный интервал между запросами
            if (_requestTimes.Count > 0 && waitTime == null)
            {
                var lastRequest = _requestTimes.Last();
                var timeSinceLastRequest = now - lastRequest;

                if (timeSinceLastRequest.TotalMilliseconds < MinIntervalBetweenRequestsMs)
                {
                    var intervalWaitTime =
                        TimeSpan.FromMilliseconds(MinIntervalBetweenRequestsMs)
                        - timeSinceLastRequest;
                    logger.LogDebug(
                        "Ожидание минимального интервала между запросами: {WaitTime}ms",
                        intervalWaitTime.TotalMilliseconds
                    );

                    waitTime = intervalWaitTime;
                }
            }
        }
        finally
        {
            _queueSemaphore.Release();
        }

        // Ожидаем вне блокировки
        if (waitTime.HasValue && waitTime.Value > TimeSpan.Zero)
        {
            await Task.Delay(waitTime.Value);
        }
    }

    private async Task RecordRequest(DateTime requestTime)
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            _requestTimes.Enqueue(requestTime);
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
        _queueSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
}
