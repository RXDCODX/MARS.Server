using System.Collections.Concurrent;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.ClientMessages.SignalRAlerts;

public class TwitchMessagesHubAwaker : BackgroundService
{
    private readonly ITwitchClient _client;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    private readonly CancellationToken _token;

    public TwitchMessagesHubAwaker(
        ITwitchClient client,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IHostApplicationLifetime lifetime,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _client = client;
        _hubContext = hubContext;
        _lifetime = lifetime;
        _dbContextFactory = dbContextFactory;
        _token = lifetime.ApplicationStopping;
        Instance = client;
    }

    private static ITwitchClient? Instance { get; set; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _lifetime.ApplicationStarted.Register(() =>
        {
            _client.OnMessageReceived += ClientOnOnMessageReceived;
            _client.OnMessageReceived += ClientKeyTriggerAlert;

            _client.OnMessageCleared += ClientOnOnMessageCleared;
        });

        return Task.CompletedTask;
    }

    private async void ClientKeyTriggerAlert(object? sender, OnMessageReceivedArgs e)
    {
        if (
            e.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && !TwitchExstension.BlackList.Any(t =>
                t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                        _token
                    );

                    var alerts = (
                        await dbContext
                            .Alerts.AsNoTracking()
                            .Where(mediaInfo =>
                                !string.IsNullOrWhiteSpace(mediaInfo.TextInfo.TriggerWord)
                            )
                            .ToListAsync(cancellationToken: _token)
                    )
                        .Where(info =>
                        {
                            var message = e.ChatMessage.Message.Trim();
                            var words = info.TextInfo.TriggerWord?.Trim().SplitWithQuotes(); // Ваш метод для разделения с учетом кавычек
                            var chatMessageWords = message.Split(
                                ' ',
                                StringSplitOptions.RemoveEmptyEntries
                            );

                            if (words is null || words.Length == 0)
                            {
                                return false;
                            }

                            // Проверяем отдельные слова (обычный случай)
                            var singleWordMatch = chatMessageWords.Any(t =>
                                words.Any(r => r.Equals(t, StringComparison.OrdinalIgnoreCase))
                            );

                            if (singleWordMatch)
                            {
                                return true;
                            }

                            // Проверяем фразы (если есть триггеры с пробелами)
                            var phraseTriggers = words.Where(w => w.Contains(' ')).ToArray();
                            if (phraseTriggers.Length == 0)
                            {
                                return false;
                            }

                            // Собираем сообщение в одну строку для проверки фраз
                            var fullMessage = string.Join(" ", chatMessageWords);

                            // Проверяем каждую фразу-триггер
                            foreach (var phrase in phraseTriggers)
                            {
                                if (
                                    fullMessage.Contains(phrase, StringComparison.OrdinalIgnoreCase)
                                )
                                {
                                    return true;
                                }
                            }

                            return false;
                        })
                        .ToArray();

                    switch (alerts.Length)
                    {
                        case > 1:
                        {
                            Random.Shared.Shuffle(alerts);
                            var info = alerts[0];

                            var alert = new MediaDto() { MediaInfo = info };

                            await _hubContext.Clients.All.Alert(alert);
                            break;
                        }
                        case 1:
                        {
                            var alert = new MediaDto { MediaInfo = alerts[0] };

                            await _hubContext.Clients.All.Alert(alert);
                            break;
                        }
                    }
                },
                _token
            );
        }
    }

    private async void ClientOnOnMessageCleared(object? sender, OnMessageClearedArgs args)
    {
        if (args.Channel.Equals(TwitchExstension.Channel, StringComparison.OrdinalIgnoreCase))
        {
            await Task.Factory.StartNew(
                () => _hubContext.Clients.All.DeleteMessage(args.TargetMessageId),
                _token
            );
        }
    }

    private async void ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs args)
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
            if (string.IsNullOrWhiteSpace(args.ChatMessage.CustomRewardId))
            {
                await Task.Factory.StartNew(
                    () => _hubContext.Clients.All.NewMessage(args.ChatMessage.Id, args.ChatMessage),
                    _token
                );
            }
        }
    }
}
