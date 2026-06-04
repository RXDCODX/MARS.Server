using System.Text.RegularExpressions;

namespace MARS.Server.Services.Twitch.ClientMessages.SignalRAlerts;

public class TwitchMessagesHubAwaker(
    ITwitchClient client,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IHostApplicationLifetime lifetime,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<TwitchMessagesHubAwaker> logger,
    TwitchUserEnsureService twitchUserEnsureService
) : BackgroundService
{
    private readonly CancellationToken _token = lifetime.ApplicationStopping;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += ClientOnOnMessageReceived;
            client.OnMessageReceived += ClientKeyTriggerAlert;

            client.OnMessageCleared += ClientOnOnMessageCleared;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            client.OnMessageReceived -= ClientOnOnMessageReceived;
            client.OnMessageReceived -= ClientKeyTriggerAlert;

            client.OnMessageCleared -= ClientOnOnMessageCleared;
        });

        return Task.CompletedTask;
    }

    private async Task ClientKeyTriggerAlert(object? sender, OnMessageReceivedArgs e)
    {
        if (
            e.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && !TwitchExstension.BlackList.Logins.Any(t =>
                t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(_token);

                var listAlerts = new List<MediaInfo>(
                    await dbContext.Alerts.CountAsync(cancellationToken: _token)
                );

                await foreach (
                    var info in dbContext
                        .Alerts.AsNoTracking()
                        .AsAsyncEnumerable()
                        .WithCancellation(_token)
                )
                {
                    if (!string.IsNullOrWhiteSpace(info.TextInfo.TriggerWord))
                    {
                        var message = e.ChatMessage.Message.Trim();
                        var words = info.TextInfo.TriggerWord?.Trim().SplitWithQuotes().ToList();
                        if (words is null || words.Count == 0)
                        {
                            continue;
                        }

                        // Ваш метод для разделения с учетом кавычек
                        var chatMessageWords = message.Split(
                            ' ',
                            StringSplitOptions.RemoveEmptyEntries
                        );

                        var regexWords = new List<string>(words.Count);

                        foreach (var word in words.ToList())
                        {
                            if (word.IsValidRegexString())
                            {
                                regexWords.Add(word);
                                words.Remove(word);
                            }
                        }

                        // Проверяем отдельные слова (обычный случай)
                        var singleWordMatch = chatMessageWords.Any(t =>
                            words.Any(r => r.Equals(t, StringComparison.OrdinalIgnoreCase))
                        );

                        if (singleWordMatch)
                        {
                            listAlerts.Add(info);
                        }

                        foreach (var regexWord in regexWords)
                        {
                            if (
                                Regex.IsMatch(message, regexWord)
                                || chatMessageWords.Any(t => Regex.IsMatch(t, regexWord))
                            )
                            {
                                listAlerts.Add(info);
                            }
                        }

                        // Проверяем фразы (если есть триггеры с пробелами)
                        var phraseTriggers = words.Where(w => w.Contains(' ')).ToArray();
                        if (phraseTriggers.Length == 0)
                        {
                            continue;
                        }

                        // Проверяем каждую фразу-триггер
                        foreach (var phrase in phraseTriggers)
                        {
                            if (message.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                            {
                                listAlerts.Add(info);
                            }
                        }
                    }
                }

                MediaInfo[] alerts = listAlerts.ToArray();

                switch (alerts.Length)
                {
                    case > 1:
                    {
                        Random.Shared.Shuffle(alerts);
                        var info = alerts[0];

                        var alert = new MediaDto { MediaInfo = info };

                        var user = await twitchUserEnsureService.EnsureUserExistsAsync(
                            TwitchUser.FromChatMessage(e.ChatMessage)!,
                            _token
                        );

                        alert.MediaInfo.FixAlertText(user, e.ChatMessage.Message);
                        alert.MediaInfo.FixAlertColor(user);

                        await hubContext.Clients.All.Alert(alert);
                        break;
                    }
                    case 1:
                    {
                        var alert = new MediaDto { MediaInfo = alerts[0] };

                        var user = await twitchUserEnsureService.EnsureUserExistsAsync(
                            TwitchUser.FromChatMessage(e.ChatMessage)!,
                            _token
                        );

                        alert.MediaInfo.FixAlertText(user, e.ChatMessage.Message);
                        alert.MediaInfo.FixAlertColor(user);

                        await hubContext.Clients.All.Alert(alert);
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogException(exception);
            }
        }
    }

    private async Task ClientOnOnMessageCleared(object? sender, OnMessageClearedArgs args)
    {
        if (args.Channel.Equals(TwitchExstension.Channel, StringComparison.OrdinalIgnoreCase))
        {
            await Task.Factory.StartNew(
                () => hubContext.Clients.All.DeleteMessage(args.TargetMessageId),
                _token
            );
        }
    }

    private async Task ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        if (
            args.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && !TwitchExstension.BlackList.Logins.Any(e =>
                e.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            if (string.IsNullOrWhiteSpace(args.ChatMessage.CustomRewardId))
            {
                await Task.Factory.StartNew(
                    () => hubContext.Clients.All.NewMessage(args.ChatMessage.Id, args.ChatMessage),
                    _token
                );
            }
        }
    }
}
