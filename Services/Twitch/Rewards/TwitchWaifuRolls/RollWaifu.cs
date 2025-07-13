using System.Globalization;
using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.WaifuRoll;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

public class RollWaifu(
    ILogger<RollWaifu> logger,
    ITwitchClient client,
    WaifuRollService waifuRollService,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> factory,
    ITwitchAPI api,
    IHostApplicationLifetime lifetime
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "rollwaifu";
    public override string DisplayName => "Roll Waifu";
    public override string Description => "Ролл вайфу через Twitch";
    public override bool IsServiceActive { get; set; }

    public async Task RollWaifuTwitchEvent(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        ChannelPointsCustomRewardRedemption? twEvent = args.Notification.Payload.Event;
        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            ) && IsServiceActive
        )
        {
            if (twEvent.Reward.Cost == 4)
            {
                Waifu? waifu = await waifuRollService.RollTheWaifu(
                    twEvent.UserId,
                    twEvent.UserName
                );

                if (waifu is not null)
                {
                    var color = await api.Helix.Chat.GetUserChatColorAsync([twEvent.UserId]);
                    await using AppDbContext dbContext2 = await factory.CreateDbContextAsync();
                    var husband =
                        await dbContext2.Hosts.FindAsync(twEvent.UserId)
                        ?? throw new NullReferenceException();
                    await hubContext.Clients.All.WaifuRoll(
                        waifu,
                        twEvent.UserName,
                        husband,
                        color.Data[0]?.Color
                    );
                    return;
                }

                await using AppDbContext dbContext = await factory.CreateDbContextAsync();
                var hostRoolWaifu = await dbContext
                    .Hosts.Include(host1 => host1.HostCoolDown)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.TwitchId == twEvent.UserId);
                var time = hostRoolWaifu?.HostCoolDown?.Time.ToOffset(TimeSpan.FromHours(3));

                if (time != null)
                {
                    DateTimeOffset notNullTime = time.Value;
                    TimeSpan wasteTime =
                        notNullTime.AddHours(1)
                        - DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));

                    var culture = CultureInfo.GetCultureInfo("ru-RU");
                    var message =
                        $"@{{user}}, Кулдаун ({(wasteTime.Hours != 0 ? wasteTime.Hours.ToString(culture) + ":" : null)}{wasteTime.Minutes.ToString(culture)}:{wasteTime.Seconds.ToString(culture)})!";
                    message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        twEvent.UserName,
                        message,
                        null,
                        null,
                        waifu
                    );
                    await client.SendMessageToMainTwitchAsync(message, logger);
                }
            }
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            lifetime.ApplicationStarted.Register(() =>
            {
                EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd += RollWaifuTwitchEvent;
            });
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd -= RollWaifuTwitchEvent;

        return base.StopAsync(cancellationToken);
    }
}
