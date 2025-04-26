using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.SoundBarService;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.Rewards.TwitchAlerts;

public class TwitchMediaAlerts : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly SoundBarFactory _soundBarFactory;
    private readonly CancellationToken _token;

    public TwitchMediaAlerts(
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ITwitchClient client,
        IHostApplicationLifetime applicationLifetime,
        SoundBarFactory soundBarFactory
    )
    {
        _hubContext = hubContext;
        _dbContextFactory = dbContextFactory;
        _soundBarFactory = soundBarFactory;
        _token = applicationLifetime.ApplicationStopping;

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += TwitchClientOnNormalMessage;
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                TwitchClientOnOnMessageSend;
        });
    }

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
        )
        {
            await Task.Run(
                async () =>
                {
                    var context = await _dbContextFactory.CreateDbContextAsync(_token);

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

        await using AppDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(_token);
        var mediaList = dbContext
            .Alerts.AsNoTracking()
            .AsEnumerable()
            .Where(e => e.MetaInfo.TwitchGuid == Guid.Parse(message.CustomRewardId))
            .ToList();

        MediaInfo? mediaOld = null;

        if (mediaList.Count == 1)
        {
            mediaOld = mediaList[0];
        }
        else if (mediaList.Count > 1)
        {
            var index = Random.Shared.Next(mediaList.Count);
            mediaOld = mediaList[index];
        }

        if (mediaOld != null)
        {
            var mediaClone = mediaOld.CloneTo();
            mediaClone.FixAlertText(message.DisplayName, message.Message);

            await _hubContext.Clients.All.Alert(new MediaDto { MediaInfo = mediaClone });
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
            )
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

        await using AppDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(_token);
        var mediaList = dbContext
            .Alerts.AsNoTracking()
            .AsEnumerable()
            .Where(e => e.MetaInfo.TwitchGuid == Guid.Parse(message.Reward.Id))
            .ToList();

        MediaInfo? mediaOld = null;

        if (mediaList.Count == 1)
        {
            mediaOld = mediaList[0];
        }
        else if (mediaList.Count > 1)
        {
            var index = Random.Shared.Next(mediaList.Count);
            mediaOld = mediaList[index];
        }

        if (mediaOld != null)
        {
            var mediaClone = mediaOld.CloneTo();
            mediaClone.FixAlertText(message.UserName, message.UserInput);

            await _hubContext.Clients.All.Alert(new MediaDto { MediaInfo = mediaClone });
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
