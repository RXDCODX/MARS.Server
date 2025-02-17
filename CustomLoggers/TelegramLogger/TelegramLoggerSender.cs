using System.Collections.Concurrent;

namespace MARS.Server.CustomLoggers.TelegramLogger;

public class TelegramLoggerSender : IDisposable
{
    private const int MaxQueuedMessages = 1024;

    private readonly ITelegramBotClient _botClient;
    private readonly long[] _chatIds;

    private readonly BlockingCollection<string> _messageQueue = new(MaxQueuedMessages);

    private readonly Task _outputTask;

    public TelegramLoggerSender(ITelegramBotClient botClient, long[] chatIds)
    {
        // Start Telegram message queue processor
        _botClient = botClient;
        _chatIds = chatIds;

        _outputTask = Task.Factory.StartNew(ProcessLogQueue, this, TaskCreationOptions.LongRunning);
    }

    public void Dispose()
    {
        _messageQueue.CompleteAdding();

        try
        {
            _outputTask.Wait(1500);
        }
        catch (TaskCanceledException) { }
        catch (AggregateException ex)
            when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException)
        { }
    }

    public void EnqueueMessage(string message)
    {
        if (!_messageQueue.IsAddingCompleted)
            try
            {
                _messageQueue.Add(message);
                return;
            }
            catch (InvalidOperationException) { }

        // Adding is completed so just log the message
        WriteMessage(message);
    }

    private void WriteMessage(string message)
    {
        Task.Run(async () =>
        {
            try
            {
                foreach (var id in _chatIds)
                    await _botClient.SendMessage(id, message).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // ignored
            }
        });
    }

    private void ProcessLogQueue()
    {
        foreach (var message in _messageQueue.GetConsumingEnumerable())
            WriteMessage(message);
    }

    private static void ProcessLogQueue(object? state)
    {
        if (state == null)
        {
            return;
        }
        var telegramLogger = (TelegramLoggerSender)state;

        telegramLogger.ProcessLogQueue();
    }
}
