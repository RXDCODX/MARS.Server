using System.Collections.Concurrent;
using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.ServiceManager.Entitys;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class SyntheziaQueueManager(
    IVoicer voicer,
    IHostApplicationLifetime hostApplicationLifetime,
    ITwitchClient client,
    ILogger<SyntheziaQueueManager> logger
) : ManagedServiceBase(logger)
{
    private readonly ConcurrentQueue<MessageToSynthezid?> _queue = new();
    public override string ServiceName => "syntheziaqueue";
    public override string DisplayName => "Синтезатор сообщений Twitch";
    public override string Description => "Озвучка сообщений из чата Twitch";
    public override bool IsServiceActive { get; set; }

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
                        UpdateActivity();
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
#if WINDOWS
        if (voicer is SyntheziaVoicer synthVoicer)
        {
            synthVoicer.InterruptSpeech();
        }
#endif
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
                        Guid = new Guid(),
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

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            hostApplicationLifetime.ApplicationStarted.Register(() =>
            {
                client.OnMessageReceived += HandMessageToVoice;
            });
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await base.StopAsync(cancellationToken);

        client.OnMessageReceived -= HandMessageToVoice;
    }

    public override List<ServiceCommandInfo> GetAvailableCommands()
    {
        return
        [
            new ServiceCommandInfo
            {
                Command = "interrupt",
                DisplayName = "Прервать озвучку",
                Description = "Остановить текущую озвучку немедленно",
            },
        ];
    }

    public override Task<bool> ExecuteCommandAsync(string command)
    {
        if (command == "interrupt")
        {
#if WINDOWS
            if (voicer is SyntheziaVoicer synthVoicer)
            {
                synthVoicer.InterruptSpeech();
                logger.LogInformation("Озвучка прервана по команде.");
                return Task.FromResult(true);
            }
#endif
        }
        return Task.FromResult(false);
    }
}
