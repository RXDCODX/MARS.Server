using System.Globalization;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.WaifuRoll;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

public class RollWaifu
{
    private readonly ILogger<RollWaifu> _logger;
    private readonly ITwitchClient _client;
    private readonly WaifuRollService _waifuRollService;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ITwitchAPI _api;

    public RollWaifu(
        ILogger<RollWaifu> logger,
        ITwitchClient client,
        WaifuRollService waifuRollService,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IDbContextFactory<AppDbContext> factory,
        ITwitchAPI api,
        IHostApplicationLifetime lifetime
    )
    {
        _logger = logger;
        _client = client;
        _waifuRollService = waifuRollService;
        _hubContext = hubContext;
        _factory = factory;
        _api = api;

        lifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd += RollWaifuTwitchEvent;
        });
    }

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
                Waifu? waifu = await _waifuRollService.RollTheWaifu(
                    twEvent.UserId,
                    twEvent.UserName
                );

                if (waifu is not null)
                {
                    var color = await _api.Helix.Chat.GetUserChatColorAsync([twEvent.UserId]);
                    await _hubContext.Clients.All.WaifuRoll(
                        waifu,
                        twEvent.UserName,
                        color.Data[0]?.Color
                    );
                    return;
                }

                await using AppDbContext dbContext = await _factory.CreateDbContextAsync();
                Host? hostRoolWaifu = await dbContext
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
                    await _client.SendMessageToMainTwitchAsync(message, _logger);
                }
            }
        }
    }
}
