using MARS.Server.Services.Twitch.FumoFriday.Entitys;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.FumoFriday;

public class FumoFridayWorker
{
    private readonly CancellationToken _cancellationToken;

    private readonly List<string> _users = new();
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _alertsHub;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<FumoFridayWorker> _logger;
    private readonly ITwitchClient _twitchClient;

    public FumoFridayWorker(
        IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<FumoFridayWorker> logger,
        IHostApplicationLifetime hostApplicationLifetime,
        ITwitchClient twitchClient
    )
    {
        _alertsHub = alertsHub;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _twitchClient = twitchClient;
        _cancellationToken = hostApplicationLifetime.ApplicationStopping;

        hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            twitchClient.OnMessageReceived += OnMessageReceived;
        });
    }

    public async void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var name = e.ChatMessage.DisplayName;
        var id = e.ChatMessage.UserId;
        var now = DateTimeOffset.Now;

        if (!_users.Contains(id) && e.ChatMessage.Channel == TwitchExstension.Channel)
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                        _cancellationToken
                    );

                    var fumoUser = await dbContext.FumoUsers.FindAsync(id, _cancellationToken);

                    if (
                        fumoUser != null
                        && now - fumoUser.LastTime > TimeSpan.FromHours(24)
                        && now.DayOfWeek == DayOfWeek.Friday
                    )
                    {
                        await _alertsHub.Clients.All.FumoFriday(
                            name,
                            e.ChatMessage.Color.ToString()
                        );
                        _users.Add(id);
                    }
                },
                _cancellationToken
            );
        }
    }

    public async Task OnRewardRedemption(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        if (args.Notification.Payload.Event.BroadcasterUserLogin != TwitchExstension.Channel)
        {
            return;
        }

        await Task.Factory.StartNew(
            async () =>
            {
                if (args.Notification.Payload.Event.Reward.Cost == 13)
                {
                    var name = args.Notification.Payload.Event.UserName;

                    if (_users.Contains(name))
                    {
                        await _twitchClient.SendMessageToMainTwitchAsync(
                            "Ты уже подписан на Fumo Friday",
                            _logger
                        );
                        return;
                    }

                    try
                    {
                        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                            _cancellationToken
                        );
                        var id = args.Notification.Payload.Event.UserId;
                        var now = DateTimeOffset.Now;

                        var isExists = await dbContext.FumoUsers.AnyAsync(
                            e => e.TwitchId == id,
                            cancellationToken: _cancellationToken
                        );

                        if (!isExists)
                        {
                            var host = new FumoUser()
                            {
                                TwitchId = id,
                                LastTime = now,
                                DisplayName = name,
                            };

                            await dbContext.FumoUsers.AddAsync(host, _cancellationToken);
                            await dbContext.SaveChangesAsync(_cancellationToken);

                            if (now.DayOfWeek == DayOfWeek.Friday)
                            {
                                await _alertsHub.Clients.All.FumoFriday(name);
                                _users.Add(id);
                            }
                        }
                        else
                        {
                            await _twitchClient.SendMessageToMainTwitchAsync(
                                $"@{name}, Ты уже счастливый фанат фум!",
                                _logger
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex);
                    }
                }
            },
            _cancellationToken
        );
    }
}
