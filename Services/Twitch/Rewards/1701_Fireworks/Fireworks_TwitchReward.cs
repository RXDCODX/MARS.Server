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

namespace MARS.Server.Services.Twitch.Rewards._1701_Fireworks;

public class Fireworks_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Fireworks_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    RickRollerService rickRollerService,
    ITwitchClient client,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎆 Феерверк!";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(0, 255, 47);
    public override int Cost { get; init; } = 1701;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    public bool IsServiceActive { get; set; } = true;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);

        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        });
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;
        await base.StopAsync(cancellationToken);
    }

    private async Task OnChannelPointsCustomRewardRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var vr = await validator
            .ForRedemption(args)
            .RequireBroadcasterUserId()
            .RequireServiceActive(IsServiceActive)
            .RequireCost(Cost)
            .ValidateAsync();

        if (vr.IsInvalid)
        {
            await client.SendMessageToMainTwitchAsync($"@{args.Payload.Event.UserName}, " + vr.FirstError);
            return;
        }

        await rickRollerService.TryRickRollAsync(
            TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
            () => hubContext.Clients.All.MakeScreenParticles(TwitchScreenParticles.Fireworks)
        );
    }
}
