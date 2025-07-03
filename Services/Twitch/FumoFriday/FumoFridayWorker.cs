using MARS.Server.Services.Twitch.FumoFriday.Entitys;
using MARS.Server.Services.Twitch.Management;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.FumoFriday;

public class FumoFridayWorker : BackgroundService
{
    private readonly CancellationToken _cancellationToken;

    private readonly List<string> _users = [];
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _alertsHub;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<FumoFridayWorker> _logger;
    private readonly ITwitchClient _twitchClient;
    private readonly ITwitchAPI _twitchApi;

    public FumoFridayWorker(
        IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<FumoFridayWorker> logger,
        IHostApplicationLifetime hostApplicationLifetime,
        ITwitchClient twitchClient,
        ITwitchAPI twitchApi
    )
    {
        _alertsHub = alertsHub;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _twitchClient = twitchClient;
        _twitchApi = twitchApi;
        _cancellationToken = hostApplicationLifetime.ApplicationStopping;

        hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            twitchClient.OnMessageReceived += OnMessageReceived;
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd += OnRewardRedemption;
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
                        var color = string.IsNullOrWhiteSpace(e.ChatMessage.ColorHex)
                            ? (await GetColor(e.ChatMessage.UserId))
                            : e.ChatMessage.ColorHex;
                        await _alertsHub.Clients.All.FumoFriday(name, color);
                        _users.Add(id);

                        fumoUser.LastTime = now;
                        await dbContext.SaveChangesAsync(_cancellationToken);
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
                    var id = args.Notification.Payload.Event.UserId;

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
                        var now = DateTimeOffset.Now;

                        var isExists = await dbContext.FumoUsers.AnyAsync(
                            e => e.TwitchId == id,
                            _cancellationToken
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
                                var color = await GetColor(id);
                                await _alertsHub.Clients.All.FumoFriday(name, color);
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

    private async Task<string?> GetColor(string id)
    {
        try
        {
            var aa = await _twitchApi.Helix.Chat.GetUserChatColorAsync([id]);
            return aa.Data[0].Color;
        }
        catch (Exception)
        {
            return null;
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
