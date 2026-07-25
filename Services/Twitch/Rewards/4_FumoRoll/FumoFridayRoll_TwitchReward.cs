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
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._4_FumoRoll;

public class FumoFridayRoll_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<FumoFridayRoll_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    FumoRollService fumoRollService,
    FumoCollectionService collectionService,
    RollCooldownService cooldownService,
    ITwitchAPI api,
    ITwitchClient client,
    TwitchUserEnsureService ensureService,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    private const string RollType = "Fumo";
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(20);

    public override string AlertDisplayName { get; set; } = "🧸 Fumo Roulette";

    public override string AlertDescription { get; set; } = "Крути рулетку Fumo по пятницам! ♪";

    public override Color Color { get; set; } = Color.FromArgb(255, 182, 193);

    public override int Cost { get; init; } = 4;

    protected override bool IsRewardActive => IsRewardEnabled();

    public override Func<bool> IsRewardEnabled { get; set; } =
        () => DateTime.Now.DayOfWeek == DayOfWeek.Friday;

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
                    $"@{args.Payload.Event.UserName}, кулдаун на FumoRoll! Подожди ещё {(int)remaining.TotalMinutes} мин.",
                    logger
                );
                return;
            }

            var fumo = await fumoRollService.RollTheFumo();

            if (fumo is not null)
            {
                var user = await ensureService.EnsureUserExistsAsync(args.Payload.Event.UserId);

                // Collection tracking (separate DB transaction)
                var collectedCount = 0;
                var totalCount = 0;
                try
                {
                    var stats = await collectionService.RecordRollAsync(
                        args.Payload.Event.UserId,
                        fumo.MfcId
                    );
                    collectedCount = stats.CollectedCount;
                    totalCount = stats.TotalCount;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to record Fumo collection for {UserId}",
                        args.Payload.Event.UserId
                    );
                }

                await hubContext.Clients.All.FumoRoll(fumo, user, collectedCount, totalCount);
            }
            else
            {
                await client.SendMessageToMainTwitchAsync(
                    $"@{args.Payload.Event.UserName}, не удалось найти Fumo :(",
                    logger
                );
            }
        });
    }
}
