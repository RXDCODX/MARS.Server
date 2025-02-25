using System.Collections.Concurrent;
using Hangfire;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class SyntheziaQueueManager
{
    private readonly ConcurrentQueue<MessageToSynthezid?> _queue = new();
    private readonly IVoicer _voicer;

    private bool _isAppReady;

    public SyntheziaQueueManager(
        IVoicer voicer,
        IHostApplicationLifetime hostApplicationLifetime,
        ITwitchClient client
    )
    {
        _voicer = voicer;

        hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            _isAppReady = true;
            client.OnMessageReceived += HandMessageToVoice;
        });
    }

    private async Task ProcessMessages()
    {
        do
        {
            if (_isAppReady)
            {
                var isDequeued = false;
                do
                {
                    isDequeued = _queue.TryDequeue(out MessageToSynthezid? result);
                    if (isDequeued && result is not null)
                    {
                        _voicer.Sound(result);
                    }

                    await Task.Delay(500);
                } while (!isDequeued);
            }

            await Task.Delay(500);
        } while (!_queue.IsEmpty);
    }

    public void HandMessageToVoice(object? sender, OnMessageReceivedArgs args)
    {
        if (
            args.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && !TwitchExstension.BlackList.Any(e =>
                e.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var message = new MessageToSynthezid
            {
                CreationDateTime = DateTimeOffset.Now,
                Guid = new Guid(),
                Message = args.ChatMessage.Message.ReplaceLinks().ReplaceTooLongWords(),
                Name = args.ChatMessage.Username,
            };

            var eventId = BackgroundJob.Enqueue(() => _queue.Enqueue(message));
            BackgroundJob.ContinueJobWith(eventId, () => ProcessMessages());
        }
    }
}
