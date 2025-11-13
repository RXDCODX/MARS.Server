using System.Globalization;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

public class RollWaifu(
    ILogger<RollWaifu> logger,
    ITwitchClient client,
    WaifuRollService waifuRollService,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> factory,
    ITwitchAPI api,
    EventSubWebsocketClient wsClient,
    WaifuRollEnsurenceService waifuDbHelper
) : BackgroundService, ITwitchReward
{
    public bool IsServiceActive { get; set; } = true;
    public int Cost { get; init; } = 4;

    public async Task RollWaifuTwitchEvent(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        ChannelPointsCustomRewardRedemption? twEvent = args.Payload.Event;
        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            ) && IsServiceActive
        )
        {
            if (twEvent.Reward.Cost == Cost)
            {
                Waifu? waifu = await waifuRollService.RollTheWaifu(
                    twEvent.UserId,
                    twEvent.UserName
                );

                if (waifu is not null)
                {
                    // Убеждаемся, что поля аниме и манги заполнены
                    waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(waifu);

                    var color = await api.Helix.Chat.GetUserChatColorAsync([twEvent.UserId]);
                    await using AppDbContext dbContext2 = await factory.CreateDbContextAsync();

                    // Загружаем Host с TwitchUser
                    var husband =
                        await dbContext2
                            .Hosts.Include(h => h.TwitchUser)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(h => h.TwitchId == twEvent.UserId)
                        ?? throw new NullReferenceException("Host не найден");

                    // Проверяем что TwitchUser загружен
                    if (husband.TwitchUser == null)
                    {
                        throw new InvalidOperationException(
                            $"TwitchUser не найден для Host {twEvent.UserId}"
                        );
                    }

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
                    var cooldown = await waifuRollService.GetWaifuRollCoolDownAsync();
                    TimeSpan wasteTime =
                        notNullTime.Add(cooldown)
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd += RollWaifuTwitchEvent;
        }

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= RollWaifuTwitchEvent;
        await base.StopAsync(cancellationToken);
    }
}
