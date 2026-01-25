using System.Collections.Concurrent;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class SyntheziaQueueManager(
    IVoicer voicer,
    ITwitchClient client,
    ILogger<SyntheziaQueueManager> logger
) : BackgroundService
{
    private readonly ConcurrentQueue<MessageToSynthezid?> _queue = new();
    public bool IsServiceActive { get; set; } = true;

    private string _lastMessage = string.Empty;
    private bool _isRepeatMessageSad = false;
    private MessageToSynthezid _repeatSynthezid = new()
    {
        CreationDateTime = DateTimeOffset.Now,
        Guid = Guid.NewGuid(),
        Message = "Не хочу повторять ваши пасты",
        Name = "CatisaAi",
    };

    private async Task ProcessMessages()
    {
        do
        {
            if (IsServiceActive)
            {
                bool isDequeued;
                do
                {
                    isDequeued = _queue.TryDequeue(out var result);
                    if (isDequeued && result is not null)
                    {
                        await voicer.Sound(result);
                    }

                    await Task.Delay(500);
                } while (!isDequeued);
            }

            await Task.Delay(500);
        } while (!_queue.IsEmpty);
    }

    /// <summary>
    /// Мгновенно останавливает озвучку и блокирует возможность озвучивать новые сообщения
    /// </summary>
    public async Task StopAndBlockAsync()
    {
        await voicer.Stop();
        logger.LogInformation("Озвучка остановлена и заблокирована.");
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
            && IsServiceActive
            && voicer.IsActive
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
                    message = new MessageToSynthezid
                    {
                        CreationDateTime = DateTimeOffset.Now,
                        Guid = Guid.NewGuid(),
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            client.OnMessageReceived += HandMessageToVoice;
        }

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived -= HandMessageToVoice;
        await base.StopAsync(cancellationToken);
    }
}
