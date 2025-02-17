using MARS.Server.Services.Twitch.FumoFriday.Entitys;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.FumoFriday;

public class FumoFridayWorker(
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<FumoFridayWorker> logger,
    IHostApplicationLifetime hostApplicationLifetime,
    ITwitchClient twitchClient
)
{
    private readonly CancellationToken _cancellationToken =
        hostApplicationLifetime.ApplicationStopping;

    private readonly List<string> _users = new();

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
                        await alertsHub.Clients.All.FumoFriday(
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
                        await twitchClient.SendMessageToPyrokxnezxzAsync(
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
                                await alertsHub.Clients.All.FumoFriday(name);
                                _users.Add(id);
                            }
                        }
                        else
                        {
                            await twitchClient.SendMessageToPyrokxnezxzAsync(
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
}
