using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Hubs.AudioControllerHub;

/// <summary>
/// Tracks pending request-response pairs for commands sent to AudioController.
/// </summary>
public class AudioControllerCommandTracker
{
    private readonly ConcurrentDictionary<
        string,
        TaskCompletionSource<AudioControllerResponse>
    > _pending = new();

    public string CreateCommand()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<AudioControllerResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pending[correlationId] = tcs;
        return correlationId;
    }

    public async Task<AudioControllerResponse> AwaitResponseAsync(
        string correlationId,
        TimeSpan? timeout = null
    )
    {
        if (!_pending.TryGetValue(correlationId, out var tcs))
        {
            return new AudioControllerResponse
            {
                CorrelationId = correlationId,
                Success = false,
                Error = "Command not found",
            };
        }

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        try
        {
            cts.Token.Register(() =>
            {
                if (_pending.TryRemove(correlationId, out var pending))
                {
                    pending.TrySetResult(
                        new AudioControllerResponse
                        {
                            CorrelationId = correlationId,
                            Success = false,
                            Error = "Timeout",
                        }
                    );
                }
            });
            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(correlationId, out _);
        }
    }

    public bool TryComplete(string correlationId, AudioControllerResponse response)
    {
        if (_pending.TryRemove(correlationId, out var tcs))
        {
            tcs.TrySetResult(response);
            return true;
        }
        return false;
    }
}
