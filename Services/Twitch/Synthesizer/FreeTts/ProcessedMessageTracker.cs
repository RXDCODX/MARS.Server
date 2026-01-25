using System.Collections.Concurrent;

namespace MARS.Server.Services.Twitch.Synthesizer.FreeTts;

/// <summary>
/// Tracks processed synthesis messages to avoid duplicate processing
/// </summary>
public interface IProcessedMessageTracker
{
    /// <summary>
    /// Checks if message with given ID was already processed
    /// </summary>
    bool IsProcessed(long messageId);

    /// <summary>
    /// Marks message as processed
    /// </summary>
    void MarkAsProcessed(long messageId);

    /// <summary>
    /// Gets count of processed messages
    /// </summary>
    int GetProcessedCount();

    /// <summary>
    /// Clears old processed messages (older than specified duration)
    /// </summary>
    void ClearOldEntries(TimeSpan age);
}

/// <summary>
/// In-memory tracker for processed synthesis messages
/// </summary>
public class ProcessedMessageTracker : IProcessedMessageTracker
{
    private readonly ConcurrentDictionary<long, DateTime> _processedMessages = new();
    private readonly ILogger<ProcessedMessageTracker> _logger;

    public ProcessedMessageTracker(ILogger<ProcessedMessageTracker> logger)
    {
        _logger = logger;
    }

    public bool IsProcessed(long messageId)
    {
        var isProcessed = _processedMessages.ContainsKey(messageId);
        if (isProcessed)
        {
            _logger.LogDebug($"Message {messageId} was already processed");
        }
        return isProcessed;
    }

    public void MarkAsProcessed(long messageId)
    {
        _processedMessages.TryAdd(messageId, DateTime.UtcNow);
        _logger.LogInformation($"Marked message {messageId} as processed. Total processed: {_processedMessages.Count}");
    }

    public int GetProcessedCount()
    {
        return _processedMessages.Count;
    }

    public void ClearOldEntries(TimeSpan age)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(age);
        var oldEntries = _processedMessages
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var messageId in oldEntries)
        {
            _processedMessages.TryRemove(messageId, out _);
        }

        if (oldEntries.Count > 0)
        {
            _logger.LogInformation($"Cleared {oldEntries.Count} old processed messages older than {age}");
        }
    }
}
