using TwitchLib.Client.Events;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchAlerts;

public class TwitchMediaAlerts(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ITwitchClient client,
    IHostApplicationLifetime applicationLifetime,
    EventSubWebsocketClient wsClient
) : BackgroundService
{
    private readonly CancellationToken _token = applicationLifetime.ApplicationStopping;

    public bool IsServiceActive { get; set; } = true;

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

                    if (alert != null)
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
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        if (
            args.Payload.Event.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            ) && IsServiceActive
        )
        {
            var value = args.Payload.Event;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            client.OnMessageReceived += TwitchClientOnNormalMessage;
            wsClient.ChannelPointsCustomRewardRedemptionAdd += TwitchClientOnOnMessageSend;
        }

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived -= TwitchClientOnNormalMessage;
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= TwitchClientOnOnMessageSend;
        await base.StopAsync(cancellationToken);
    }
}
