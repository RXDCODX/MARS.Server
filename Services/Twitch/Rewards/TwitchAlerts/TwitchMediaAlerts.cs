using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Twitch.Management;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.Rewards.TwitchAlerts;

public class TwitchMediaAlerts(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ITwitchClient client,
    IHostApplicationLifetime applicationLifetime,
    ILogger<TwitchMediaAlerts> logger
) : ManagedServiceBase(logger)
{
    private readonly CancellationToken _token = applicationLifetime.ApplicationStopping;

    public override string ServiceName => "twitchmediaalerts";
    public override string DisplayName => "Twitch Media Alerts";
    public override string Description => "Медиа-алерты Twitch";
    public override bool IsServiceActive { get; set; }

    internal async void TwitchClientOnNormalMessage(object? sender, OnMessageReceivedArgs args)
    {
        if (
            args.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && !TwitchExstension.BlackList.Any(u =>
                u.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
            && IsServiceActive
        )
        {
            await Task.Run(
                async () =>
                {
                    var context = await dbContextFactory.CreateDbContextAsync(_token);

                    var alert = context.Alerts.FirstOrDefault(e =>
                        e.MetaInfo.TwitchGuid.ToString() == args.ChatMessage.CustomRewardId
                    );

                    if (alert != default)
                    {
                        await SendAlert(args);
                    }
                },
                _token
            );
        }
    }

    private async Task SendAlert(OnMessageReceivedArgs args)
    {
        var message = args.ChatMessage;

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(_token);
        var mediaList = dbContext
            .Alerts.AsNoTracking()
            .AsEnumerable()
            .Where(e => e.MetaInfo.TwitchGuid == Guid.Parse(message.CustomRewardId))
            .ToList();

        MediaInfo? mediaOld = null;

        switch (mediaList.Count)
        {
            case 1:
                mediaOld = mediaList[0];
                break;
            case > 1:
            {
                var index = Random.Shared.Next(mediaList.Count);
                mediaOld = mediaList[index];
                break;
            }
        }

        if (mediaOld != null)
        {
            var mediaClone = mediaOld.CloneTo();
            mediaClone.FixAlertText(message.DisplayName, message.Message);

            await hubContext.Clients.All.Alert(new MediaDto { MediaInfo = mediaClone });
        }
    }

    internal async Task TwitchClientOnOnMessageSend(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        if (
            args.Notification.Payload.Event.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            ) && IsServiceActive
        )
        {
            var value = args.Notification.Payload.Event;

            if (string.IsNullOrWhiteSpace(value.UserInput))
            {
                await SendAlert(value);
            }
        }
    }

    private async Task SendAlert(ChannelPointsCustomRewardRedemption value)
    {
        var message = value;

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(_token);
        var mediaList = dbContext
            .Alerts.AsNoTracking()
            .AsEnumerable()
            .Where(e => e.MetaInfo.TwitchGuid == Guid.Parse(message.Reward.Id))
            .ToList();

        MediaInfo? mediaOld = null;

        switch (mediaList.Count)
        {
            case 1:
                mediaOld = mediaList[0];
                break;
            case > 1:
            {
                var index = Random.Shared.Next(mediaList.Count);
                mediaOld = mediaList[index];
                break;
            }
        }

        if (mediaOld != null)
        {
            var mediaClone = mediaOld.CloneTo();
            mediaClone.FixAlertText(message.UserName, message.UserInput);

            await hubContext.Clients.All.Alert(new MediaDto { MediaInfo = mediaClone });
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            applicationLifetime.ApplicationStarted.Register(() =>
            {
                client.OnMessageReceived += TwitchClientOnNormalMessage;
                EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                    TwitchClientOnOnMessageSend;
            });
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        client.OnMessageReceived -= TwitchClientOnNormalMessage;
        EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd -=
            TwitchClientOnOnMessageSend;
        return base.StopAsync(cancellationToken);
    }
}
