using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._353_TikTokEdit;

public class TikTokEdit_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<TikTokEdit_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    RickRollerService rickRollerService,
    ITwitchClient client,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎬 Make a TikTok Edit";
    public override string AlertDescription { get; set; } =
        "📝 текст наверху и внизу можно разделить символом `=`";
    public override Color Color { get; set; } = Color.FromArgb(245, 0, 0);
    public override int Cost { get; init; } = 353;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd -=
                OnChannelPointsCustomRewardRedemption;
        });

        await base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;
        return base.StopAsync(cancellationToken);
    }

    private async Task OnChannelPointsCustomRewardRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var vr = await validator
            .ForRedemption(args)
            .RequireBroadcasterUserId()
            .RequireCost(Cost)
            .ValidateAsync();

        if (vr.IsInvalid)
        {
            await client.SendMessageToMainTwitchAsync($"@{args.Payload.Event.UserName}, " + vr.FirstError);
            return;
        }

        var text = args.Payload.Event.UserInput;

        await rickRollerService.TryRickRollAsync(
            TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
            () => hubContext.Clients.All.TikTokEdit(Guid.NewGuid(), text)
        );
    }
}
