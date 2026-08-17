using System.Drawing;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Migrations;
using MARS.Server.Services.PyroAlerts.Entitys;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchUser = MARS.Server.Services.Twitch.Entitys.TwitchUser;

namespace MARS.Server.Services.Twitch.Rewards._170_MikuMondayAlert;

public class MikuMondayAlert_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<MikuMondayAlert_TwitchReward> logger,
    IHostEnvironment environment,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> factory,
    ITwitchEventValidationService validator,
    ITwitchClient client,
    TwitchUserEnsureService twitchUserEnsureService,
    ITwitchAPI api,
    TokenService tokenService,
    RickRollerService rickRollerService
) : TemporaryReward(channelRewardsService, logger, environment)
{
    /// <summary>
    /// Статичный Guid записи Miku Monday Alert в таблице Alerts (см. миграцию SeedMikuMondayAlertMedia).
    /// </summary>
    public readonly Guid MikuMondayAlertMediaId = SeedMikuMondayAlertMedia.MikuMondayAlertMediaId;

    private static readonly TimeSpan GlobalCooldown = TimeSpan.FromMinutes(20);

    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly HashSet<string> _activatedUserIds = [];
    private string _sessionDayKey = DateTime.Now.ToString("yyyy-MM-dd");
    private DateTime _lastActivation = DateTime.MinValue;

    private protected override CreateCustomRewardsRequest CreateCustomRewardsRequest =>
        new()
        {
            Title = AlertDisplayName,
            Prompt = AlertDescription,
            Cost = Cost,
            IsEnabled = true,
            IsUserInputRequired = false,
            IsMaxPerStreamEnabled = false,
            IsMaxPerUserPerStreamEnabled = false,
            IsGlobalCooldownEnabled = true,
            ShouldRedemptionsSkipRequestQueue = false,
            GlobalCooldownSeconds = (int)GlobalCooldown.TotalSeconds,
        };

    public override string AlertDisplayName { get; set; } = "🎤 Miku Monday Alert";
    public override string AlertDescription { get; set; } =
        "🎶 Мику заглянула на стрим и хочет устроить понедельничный замес! Один раз за стрим на зрителя, общий кулдаун 20 минут для всех! 🎵";
    public override Color Color { get; set; } = Color.FromArgb(57, 197, 187);
    public override int Cost { get; init; } = 170;
    public override Func<bool> IsRewardEnabled { get; set; } =
        () => DateTime.Now.DayOfWeek == DayOfWeek.Monday;

    protected override bool IsRewardActive => IsRewardEnabled();

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd -=
                OnChannelPointsCustomRewardRedemption;
        });

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancelToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;
        await base.StopAsync(cancelToken);
    }

    internal async Task OnChannelPointsCustomRewardRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var vr = await validator
            .ForRedemption(args)
            .RequireBroadcasterUserId()
            .RequireCost(Cost)
            .RequireFollower()
            .ValidateWithResponseAsync(args.Payload.Event.UserName);

        if (vr.IsInvalid)
        {
            return;
        }

        var twEvent = args.Payload.Event;

        await rickRollerService.TryRickRollAsync(
            TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
            async () =>
            {
                var now = DateTime.Now;

                var isAllowed = await TryMarkActivationAsync(args, twEvent, now);
                if (!isAllowed)
                {
                    return;
                }

                await PlayAlertAsync(args, twEvent);
            }
        );
    }

    /// <summary>
    /// Проверяет лимиты активации: 1 раз за стрим на пользователя и общий кулдаун 20 минут.
    /// Возвращает true, если активация разрешена.
    /// </summary>
    private async Task<bool> TryMarkActivationAsync(
        ChannelPointsCustomRewardRedemptionArgs args,
        ChannelPointsCustomRewardRedemption twEvent,
        DateTime now
    )
    {
        var result = false;

        await _semaphore.WaitAsync();

        try
        {
            ResetIfNewDay();

            if (_activatedUserIds.Contains(twEvent.UserId))
            {
                await RefundRedemptionAsync(args, twEvent.UserName);
                return result;
            }

            if (now - _lastActivation < GlobalCooldown)
            {
                var remaining = (int)(GlobalCooldown - (now - _lastActivation)).TotalSeconds;
                await client.SendMessageToMainTwitchAsync(
                    $"@{twEvent.UserName}, общий кулдаун Miku Monday Alert! Осталось {remaining} сек.",
                    logger
                );
                return result;
            }

            _lastActivation = now;
            _activatedUserIds.Add(twEvent.UserId);
            result = true;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
        finally
        {
            _semaphore.Release();
        }

        return result;
    }

    private async Task PlayAlertAsync(
        ChannelPointsCustomRewardRedemptionArgs args,
        ChannelPointsCustomRewardRedemption twEvent
    )
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var media = await dbContext
                .Alerts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == MikuMondayAlertMediaId);

            if (media == null)
            {
                logger.LogError(
                    "Miku Monday Alert: не найдена запись в Alerts с Id {AlertId}",
                    MikuMondayAlertMediaId
                );
                return;
            }

            var mediaClone = media.CloneTo();

            var twitchUser = await twitchUserEnsureService.EnsureUserExistsAsync(
                TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!
            );

            mediaClone.FixAlertText(twitchUser, string.Empty);
            mediaClone.FixAlertColor(twitchUser);

            await hubContext.Clients.All.Alert(new MediaDto { MediaInfo = mediaClone });

            await client.SendMessageToMainTwitchAsync(
                $"@{twEvent.UserName} активировал Miku Monday Alert! 🎤",
                logger
            );

            logger.LogInformation(
                "Miku Monday Alert активирован пользователем {UserName} за {Cost} баллов",
                twEvent.UserName,
                twEvent.Reward.Cost
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }

    /// <summary>
    /// Возвращает баллы пользователю, если он уже активировал награду в этом стриме.
    /// </summary>
    private async Task RefundRedemptionAsync(
        ChannelPointsCustomRewardRedemptionArgs args,
        string userName
    )
    {
        try
        {
            var accessToken = tokenService.Token?.AccessToken;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                logger.LogWarning("Miku Monday Alert: отсутствует токен для возврата баллов");
                return;
            }

            await api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                TwitchExstension.ChannelId,
                args.Payload.Event.Reward.Id,
                [args.Payload.Event.Id],
                new UpdateCustomRewardRedemptionStatusRequest
                {
                    Status = CustomRewardRedemptionStatus.CANCELED,
                },
                accessToken
            );

            await client.SendMessageToMainTwitchAsync(
                $"@{userName}, ты уже активировал Miku Monday Alert в этом стриме! Баллы возвращены.",
                logger
            );

            logger.LogInformation(
                "Miku Monday Alert: возвращены баллы пользователю {UserName}",
                userName
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }

    /// <summary>
    /// Сбрасывает список активировавших пользователей при смене даты.
    /// </summary>
    private void ResetIfNewDay()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        if (_sessionDayKey != today)
        {
            _sessionDayKey = today;
            _activatedUserIds.Clear();
        }
    }
}
