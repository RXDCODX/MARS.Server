using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Twitch.FumoFriday.Entitys;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.FumoFriday;

public class FumoFridayWorker(
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<FumoFridayWorker> logger,
    IHostApplicationLifetime hostApplicationLifetime,
    ITwitchClient twitchClient,
    ITwitchAPI twitchApi,
    EventSubWebsocketClient wsClient
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "fumofriday";
    public override string DisplayName => "Fumo Friday";
    public override string Description => "Fumo Friday Twitch интеграция";
    public override bool IsServiceActive { get; set; }

    private readonly CancellationToken _cancellationToken =
        hostApplicationLifetime.ApplicationStopping;

    private readonly List<string> _users = [];

    public async void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (!IsServiceActive)
        {
            return;
        }

        var name = e.ChatMessage.DisplayName;
        var id = e.ChatMessage.UserId;
        var now = DateTimeOffset.Now;

        if (!_users.Contains(id) && e.ChatMessage.Channel == TwitchExstension.Channel)
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    await using var dbContext = await dbContextFactory.CreateDbContextAsync(
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
                        await alertsHub.Clients.All.FumoFriday(name, color);
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
        if (!IsServiceActive)
        {
            return;
        }

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
                        await twitchClient.SendMessageToMainTwitchAsync(
                            "Ты уже подписан на Fumo Friday",
                            logger
                        );
                        return;
                    }

                    try
                    {
                        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
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
                                await alertsHub.Clients.All.FumoFriday(name, color);
                                _users.Add(id);
                            }
                        }
                        else
                        {
                            await twitchClient.SendMessageToMainTwitchAsync(
                                $"@{name}, Ты уже счастливый фанат фум!",
                                logger
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogException(ex);
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
            var aa = await twitchApi.Helix.Chat.GetUserChatColorAsync([id]);
            return aa.Data[0].Color;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            twitchClient.OnMessageReceived += OnMessageReceived;
            wsClient.ChannelPointsCustomRewardRedemptionAdd += OnRewardRedemption;
        });

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        twitchClient.OnMessageReceived -= OnMessageReceived;
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnRewardRedemption;

        return base.StopAsync(cancellationToken);
    }
}
