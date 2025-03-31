using System.Globalization;
using MARS.Server.Services.WaifuRoll;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

public class RollWaifu(
    ILogger<RollWaifu> logger,
    ITwitchClient client,
    WaifuRollService waifuRollService,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> factory
)
{
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
            )
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
                    await hubContext.Clients.All.WaifuRoll(waifu, twEvent.UserName);
                    return;
                }

                await using AppDbContext dbContext = await factory.CreateDbContextAsync();
                Host? hostRoolWaifu = await dbContext
                    .Hosts.Include(host1 => host1.HostCoolDown)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.TwitchId == twEvent.UserId);
                var time = hostRoolWaifu?.HostCoolDown?.Time;

                if (time != null)
                {
                    DateTimeOffset notNullTime = time.Value;
                    TimeSpan wasteTime = notNullTime.AddHours(1) - DateTimeOffset.Now;

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
}
