using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.Mapping;
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
        TwitchMessagesHubAwaker.Instance = client;
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
        )
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                        _token
                    );

                    var mediaInfo = await dbContext.Alerts.AsNoTracking().ToArrayAsync(_token);

                    var alerts = mediaInfo
                        .Where(info =>
                        {
                            if (info.TextInfo.TriggerWord == null)
                            {
                                return false;
                            }

                            var words = info.TextInfo.TriggerWord.Split(' ');
                            var textWords = e.ChatMessage.Message.Split(' ');
                            var isExists = textWords.Any(t =>
                                words.Any(r => r.Equals(t, StringComparison.OrdinalIgnoreCase))
                            );

                            return isExists;
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
