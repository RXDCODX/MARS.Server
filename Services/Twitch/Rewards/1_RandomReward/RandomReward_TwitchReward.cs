using System.Drawing;
using System.Reflection;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.Models;
using TwitchLib.EventSub.Core.Models.ChannelPoints;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._1_RandomReward;

public class RandomReward_TwitchReward(
    ITwitchClient client,
    IHostApplicationLifetime lifetime,
    IHostEnvironment hostEnvironment,
    ILogger<RandomReward_TwitchReward> logger,
    EventSubWebsocketClient wsClient,
    ChannelRewardsService channelRewardsService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptionsMonitor<TwitchRewardsOptions> rewardsOptions
) : TemporaryReward(channelRewardsService, logger, hostEnvironment)
{
    public override string AlertDisplayName { get; set; } = "🎲 Случайная награда!";
    public override string AlertDescription { get; set; } =
        "🎲 Активирует 1 случайную награду канала!";
    public override Color Color { get; set; }
    public override int Cost { get; init; } = 1;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    private static readonly List<int> RecentCosts = [];
    private static readonly Lock RecentCostsLock = new();

    public override Task StartAsync(CancellationToken stoppingToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd +=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;

        return base.StartAsync(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;

        return base.StopAsync(cancellationToken);
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;

        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
            && twEvent.Reward.Cost == Cost
            && IsRewardEnabled()
        )
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        await ActivateRandomReward(sender, twEvent);
                    }
                    catch (Exception e)
                    {
                        logger.LogException(e);
                    }
                },
                _cancellationToken
            );
        }
    }

    private async Task ActivateRandomReward(
        object? sender,
        ChannelPointsCustomRewardRedemption originalEvent
    )
    {
        var candidateCosts = new HashSet<int>();

        // a) Coded rewards — collect costs from handlers subscribed to EventSub
        var eventField = typeof(EventSubWebsocketClient).GetField(
            "ChannelPointsCustomRewardRedemptionAdd",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        var eventDelegate = (MulticastDelegate?)eventField?.GetValue(wsClient);

        if (eventDelegate != null)
        {
            foreach (var handler in eventDelegate.GetInvocationList())
            {
                if (handler.Target is TemporaryReward reward && reward.Cost != Cost)
                {
                    candidateCosts.Add(reward.Cost);
                }
            }
        }

        // b) DB Alerts — collect distinct TwitchPointsCost
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        var alertCosts = await dbContext
            .Alerts.AsNoTracking()
            .Where(a => a.MetaInfo.IsEnabled && a.MetaInfo.TwitchPointsCost > 0)
            .Select(a => a.MetaInfo.TwitchPointsCost)
            .Distinct()
            .ToListAsync(_cancellationToken);

        foreach (var c in alertCosts)
        {
            candidateCosts.Add(c);
        }

        // c) Exclude self
        candidateCosts.Remove(Cost);

        // d) Exclude by config
        var excluded = rewardsOptions.CurrentValue?.ExcludeFromRandomPool;

        if (excluded != null)
        {
            foreach (var ex in excluded)
            {
                candidateCosts.Remove(ex);
            }
        }

        // e) Exclude last 7 used
        lock (RecentCostsLock)
        {
            foreach (var recent in RecentCosts)
            {
                candidateCosts.Remove(recent);
            }
        }

        if (candidateCosts.Count == 0)
        {
            await client.SendMessageToMainTwitchAsync(
                $"@{originalEvent.UserName}, нет доступных случайных наград :(",
                logger
            );

            return;
        }

        // 2. Pick random
        var chosenCost = candidateCosts.ElementAt(Random.Shared.Next(candidateCosts.Count));

        // 3. Track recent
        lock (RecentCostsLock)
        {
            RecentCosts.Add(chosenCost);

            while (RecentCosts.Count > 7)
            {
                RecentCosts.RemoveAt(0);
            }
        }

        // 4. Construct fake args with the chosen cost
        var fakeArgs = new ChannelPointsCustomRewardRedemptionArgs();

        fakeArgs.Payload = new EventSubNotificationPayload<ChannelPointsCustomRewardRedemption>
        {
            Event = new ChannelPointsCustomRewardRedemption
            {
                Id = Guid.NewGuid().ToString(),
                BroadcasterUserId = TwitchExstension.ChannelId,
                BroadcasterUserName = TwitchExstension.Channel,
                BroadcasterUserLogin = TwitchExstension.Channel,
                UserId = originalEvent.UserId,
                UserName = originalEvent.UserName,
                UserLogin = originalEvent.UserLogin,
                UserInput = string.Empty,
                Status = "fulfilled",
                Reward = new RedemptionReward
                {
                    Id = Guid.NewGuid().ToString(),
                    Cost = chosenCost,
                    Title = "Random Reward",
                },
                RedeemedAt = DateTimeOffset.UtcNow,
            },
        };

        // 5. Chat notification
        await client.SendMessageToMainTwitchAsync(
            $"@{originalEvent.UserName}, активирована случайная награда за {chosenCost} баллов!",
            logger
        );

        // 6. Invoke all handlers — only the one with matching cost acts
        if (eventDelegate != null)
        {
            foreach (var handler in eventDelegate.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke(sender ?? wsClient, fakeArgs);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Ошибка при вызове handler'а награды для Cost {Cost}",
                        chosenCost
                    );
                }
            }
        }
    }
}
