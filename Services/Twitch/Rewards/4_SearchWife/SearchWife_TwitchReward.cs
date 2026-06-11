using System.Globalization;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.Rewards._4_SearchWife;

public class SearchWife_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<SearchWife_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    WaifuRollService waifuRollService,
    WaifuRollEnsurenceService waifuDbHelper,
    ITwitchAPI api,
    ITwitchClient client,
    IDbContextFactory<AppDbContext> factory,
    RickRollerService rickRollerService
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🔍 Поиск супруга";
    public override string AlertDescription { get; set; } =
        "💰 Цена - 50 кредитов. Узнать кредиты - !rank/!myrank.";
    public override Color Color { get; set; } = Color.FromArgb(24, 0, 255);
    public override int Cost { get; init; } = 4;
    public override Func<bool> IsRewardEnabled { get; set; } =
        () => DateTime.Now.DayOfWeek != DayOfWeek.Friday;

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
        ChannelPointsCustomRewardRedemption? twEvent = args.Payload.Event;
        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            if (twEvent.Reward.Cost == Cost)
            {
                await Task.Factory.StartNew(async () =>
                {
                    await rickRollerService.TryRickRollAsync(
                        TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                        async () =>
                        {
                            Waifu? waifu = await waifuRollService.RollTheWaifu(
                                twEvent.UserId,
                                twEvent.UserName
                            );

                            if (waifu is not null)
                            {
                                // Убеждаемся, что поля аниме и манги заполнены
                                waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(waifu);

                                var color = await api.Helix.Chat.GetUserChatColorAsync(
                                    [twEvent.UserId]
                                );
                                await using AppDbContext dbContext2 =
                                    await factory.CreateDbContextAsync();

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

                            await using AppDbContext dbContext =
                                await factory.CreateDbContextAsync();
                            var hostRoolWaifu = await dbContext
                                .Hosts.Include(host1 => host1.HostCoolDown)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(e => e.TwitchId == twEvent.UserId);
                            var time = hostRoolWaifu?.HostCoolDown?.Time.ToOffset(
                                TimeSpan.FromHours(3)
                            );

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
                    );
                });
            }
        }
    }
}
