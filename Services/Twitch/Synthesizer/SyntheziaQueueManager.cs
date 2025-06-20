using System.Collections.Concurrent;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class SyntheziaQueueManager : BackgroundService
{
    private readonly ConcurrentQueue<MessageToSynthezid?> _queue = new();
    private readonly IVoicer _voicer;

    private bool _isAppReady;
    private string _lastMessage = string.Empty;
    private bool _isRepeatMessageSad = false;
    private MessageToSynthezid _repeatSynthezid = new()
    {
        CreationDateTime = DateTimeOffset.Now,
        Guid = Guid.NewGuid(),
        Message = "Не хочу повторять ваши пасты",
        Name = "CatisaAi",
    };

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
                    isDequeued = _queue.TryDequeue(out var result);
                    if (isDequeued && result is not null)
                    {
                        await _voicer.Sound(result);
                    }

                    await Task.Delay(500);
                } while (!isDequeued);
            }

            await Task.Delay(500);
        } while (!_queue.IsEmpty);
    }

    public async void HandMessageToVoice(object? sender, OnMessageReceivedArgs args)
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
            await Task.Run(async () =>
            {
                var currentMessage = args.ChatMessage.Message;

                MessageToSynthezid message;

                if (_lastMessage.Equals(currentMessage))
                {
                    message = _repeatSynthezid;
                    _isRepeatMessageSad = true;
                }
                else
                {
                    message = new()
                    {
                        CreationDateTime = DateTimeOffset.Now,
                        Guid = new(),
                        Message = currentMessage
                            .Trim()
                            .CutTooLongText()
                            .ReplaceLinks()
                            .ReplaceTooLongWords(),
                        Name = args.ChatMessage.Username,
                    };

                    _repeatSynthezid = message;
                    _lastMessage = currentMessage;
                    _isRepeatMessageSad = false;
                }

                if (!_isRepeatMessageSad)
                {
                    _queue.Enqueue(message);
                    await ProcessMessages();
                }
            });
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
