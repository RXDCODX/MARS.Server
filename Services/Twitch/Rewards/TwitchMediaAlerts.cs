using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.PyroAlerts.Entitys;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards;

public class TwitchMediaAlerts(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ITwitchClient client,
    IHostApplicationLifetime applicationLifetime,
    EventSubWebsocketClient wsClient,
    RickRollerService rickRollerService,
    TwitchUserEnsureService twitchUserEnsureService,
    ITwitchEventValidationService validator
) : BackgroundService
{
    private readonly CancellationToken _token = applicationLifetime.ApplicationStopping;

    public bool IsServiceActive { get; set; } = true;

    internal async Task TwitchClientOnNormalMessage(object? sender, OnMessageReceivedArgs args)
    {
        var vr = await validator
            .ForMessageReceived(args)
            .RequireChannel()
            .SkipBlacklisted()
            .RequireServiceActive(IsServiceActive)
            .ValidateAsync();

        if (vr.IsInvalid)
        {
            return;
        }

        await Task.Run(
            async () =>
            {
                var context = await dbContextFactory.CreateDbContextAsync(_token);

                var alert = context.Alerts.FirstOrDefault(e =>
                    e.MetaInfo.IsEnabled
                    && e.MetaInfo.TwitchGuid.ToString() == args.ChatMessage.CustomRewardId
                );

                if (alert != null)
                {
                    await SendAlert(args);
                }
            },
            _token
        );
    }

    private async Task SendAlert(OnMessageReceivedArgs args)
    {
        var message = args.ChatMessage;

        if (string.IsNullOrWhiteSpace(message.CustomRewardId))
        {
            return;
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(_token);
        var mediaList = dbContext
            .Alerts.AsNoTracking()
            .AsEnumerable()
            .Where(e => e.MetaInfo.IsEnabled && e.MetaInfo.TwitchGuid == Guid.Parse(message.CustomRewardId))
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

            var user = await twitchUserEnsureService.EnsureUserExistsAsync(
                TwitchUser.FromOnMessageReceivedArgs(args)!,
                _token
            );

            mediaClone.FixAlertText(user, message.Message);
            mediaClone.FixAlertColor(user);

            await hubContext.Clients.All.Alert(new MediaDto { MediaInfo = mediaClone });
        }
    }

    internal async Task TwitchClientOnOnMessageSend(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var vr = await validator
            .ForRedemption(args)
            .RequireBroadcasterUserId()
            .RequireServiceActive(IsServiceActive)
            .ValidateAsync();

        if (vr.IsInvalid)
        {
            return;
        }

        var value = args.Payload.Event;

        if (string.IsNullOrWhiteSpace(value.UserInput))
        {
            var message = value;

            await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(
                _token
            );
            var mediaList = dbContext
                .Alerts.AsNoTracking()
                .AsEnumerable()
                .Where(e => e.MetaInfo.IsEnabled && e.MetaInfo.TwitchPointsCost == message.Reward.Cost)
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
                await rickRollerService.TryRickRollAsync(
                    TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                    async () =>
                    {
                        var mediaClone = mediaOld.CloneTo();

                        var user = await twitchUserEnsureService.EnsureUserExistsAsync(
                            TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                            _token
                        );

                        mediaClone.FixAlertText(user, string.Empty);
                        mediaClone.FixAlertColor(user);

                        await hubContext.Clients.All.Alert(
                            new MediaDto { MediaInfo = mediaClone }
                        );
                    }
                );
            }
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            client.OnMessageReceived += TwitchClientOnNormalMessage;
            wsClient.ChannelPointsCustomRewardRedemptionAdd += TwitchClientOnOnMessageSend;
        }

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived -= TwitchClientOnNormalMessage;
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= TwitchClientOnOnMessageSend;
        await base.StopAsync(cancellationToken);
    }
}
