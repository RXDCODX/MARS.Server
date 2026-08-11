using System.Drawing;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Validation;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.Entitys;
using MARS.Server.Services.WaifuRoll.helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

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
    RickRollerService rickRollerService,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🔍 Поиск супруга";
    public override string AlertDescription { get; set; } = "⠀";
    public override Color Color { get; set; } = Color.FromArgb(24, 0, 255);
    public override int Cost { get; init; } = 4;

    protected override bool IsRewardActive => IsRewardEnabled();

    public override Func<bool> IsRewardEnabled { get; set; } =
        () =>
            DateTime.Now.DayOfWeek != DayOfWeek.Friday
            && DateTime.Now.DayOfWeek != DayOfWeek.Wednesday
            && DateTime.Now.DayOfWeek != DayOfWeek.Monday;

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

        var twEvent = args.Payload.Event;

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
                        waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(waifu);

                        await using AppDbContext dbContext2 = await factory.CreateDbContextAsync();

                        var husband =
                            await dbContext2
                                .Husbands.Include(h => h.TwitchUser)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(h => h.TwitchId == twEvent.UserId)
                            ?? throw new NullReferenceException("Husband не найден");

                        if (husband.TwitchUser == null)
                        {
                            throw new InvalidOperationException(
                                $"TwitchUser не найден для Husband {twEvent.UserId}"
                            );
                        }

                        await hubContext.Clients.All.WaifuRoll(waifu, husband);
                        return;
                    }

                    await using AppDbContext dbContext = await factory.CreateDbContextAsync();
                    var hostRoolWaifu = await dbContext
                        .Husbands.Include(host1 => host1.HusbandCoolDown)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(e => e.TwitchId == twEvent.UserId);
                    var time = hostRoolWaifu?.HusbandCoolDown?.Time;

                    if (time != null)
                    {
                        DateTime notNullTime = time.Value;
                        var cooldown = await waifuRollService.GetWaifuRollCoolDownAsync();
                        TimeSpan wasteTime = notNullTime.Add(cooldown) - DateTime.Now;
                        var cooldownText = AnswersForTwitchRewards.FormatCooldownTime(wasteTime);

                        var message = $"@{{user}}, кулдаун! Подожди ещё {cooldownText} мин.";
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
