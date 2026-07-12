using System.Drawing;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._4_FrogRoll;

public class FrogRoll_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<FrogRoll_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    FrogRollService frogRollService,
    RollCooldownService cooldownService,
    ITwitchAPI api,
    ITwitchClient client,
    TwitchUserEnsureService ensureService,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    private const string RollType = "Frog";
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(20);

    public override string AlertDisplayName { get; set; } = "🐸 Frog Roulette";

    public override string AlertDescription { get; set; } = "Крути рулетку лягушек! ♪";

    public override Color Color { get; set; } = Color.FromArgb(76, 175, 80);

    public override int Cost { get; init; } = 4;

    protected override bool IsRewardActive => IsRewardEnabled();

    public override Func<bool> IsRewardEnabled { get; set; } =
        () => DateTime.Now.DayOfWeek == DayOfWeek.Wednesday;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);

        wsClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsCustomRewardRedemption;
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
            .RequireRewardEnabled(IsRewardEnabled)
            .RequireCost(Cost)
            .RequireFollower()
            .ValidateWithResponseAsync(args.Payload.Event.UserName);

        if (vr.IsInvalid)
        {
            return;
        }

        await Task.Factory.StartNew(async () =>
        {
            var (allowed, remaining) = await cooldownService.CheckAndUpdateCooldownAsync(
                args.Payload.Event.UserId,
                RollType,
                Cooldown
            );

            if (!allowed)
            {
                await client.SendMessageToMainTwitchAsync(
                    $"@{args.Payload.Event.UserName}, кулдаун на FrogRoll! Подожди ещё {(int)remaining.TotalMinutes} мин.",
                    logger
                );
                return;
            }

            var frog = await frogRollService.RollTheFrog();

            if (frog is not null)
            {
                var user = await ensureService.EnsureUserExistsAsync(args.Payload.Event.UserId);

                await hubContext.Clients.All.FrogRoll(frog, user);
            }
            else
            {
                await client.SendMessageToMainTwitchAsync(
                    $"@{args.Payload.Event.UserName}, не удалось найти лягушку :(",
                    logger
                );
            }
        });
    }
}
