using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MARS.Server.CustomLoggers.SignalRLogger;

public class LoggerHubRecursionGuard
{
    private readonly ConcurrentDictionary<string, byte> _loggerHubConnectionIds = new();
    private readonly AsyncLocal<int> _suppressionDepth = new();

    public IDisposable BeginSuppression()
    {
        return new SuppressionScope(this);
    }

    public void TrackLoggerHubConnection(string connectionId)
    {
        if (!string.IsNullOrWhiteSpace(connectionId))
        {
            _loggerHubConnectionIds[connectionId] = 0;
        }
    }

    public void UntrackLoggerHubConnection(string connectionId)
    {
        if (!string.IsNullOrWhiteSpace(connectionId))
        {
            _loggerHubConnectionIds.TryRemove(connectionId, out _);
        }
    }

    public bool ShouldSkipLog(string category, string message)
    {
        var result = false;
        var hasCategory = !string.IsNullOrWhiteSpace(category);
        var hasMessage = !string.IsNullOrWhiteSpace(message);

        if (_suppressionDepth.Value > 0)
        {
            result = true;
        }
        else if (
            hasCategory
            && category.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase)
        )
        {
            // Block all Microsoft.AspNetCore.* logs from going to the LoggerHub to avoid recursion
            result = true;
        }

        return result;
    }

    private bool HasTrackedConnectionId(string message)
    {
        var result = false;

        foreach (var connectionId in _loggerHubConnectionIds.Keys)
        {
            if (message.Contains(connectionId, StringComparison.Ordinal))
            {
                result = true;
                break;
            }
        }

        return result;
    }

    private void IncrementSuppression()
    {
        _suppressionDepth.Value = _suppressionDepth.Value + 1;
    }

    private void DecrementSuppression()
    {
        var result = _suppressionDepth.Value - 1;

        if (result < 0)
        {
            result = 0;
        }

        _suppressionDepth.Value = result;
    }

    private sealed class SuppressionScope : IDisposable
    {
        private readonly LoggerHubRecursionGuard _guard;
        private bool _disposed;

        public SuppressionScope(LoggerHubRecursionGuard guard)
        {
            _guard = guard;
            _guard.IncrementSuppression();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _guard.DecrementSuppression();
                _disposed = true;
            }
        }
    }
}
