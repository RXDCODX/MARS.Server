using System.Drawing;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._99999_AllRefundService;

public class AllRefund_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<AllRefund_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    ITwitchAPI api,
    TokenService tokenService,
    ITwitchClient client,
    TwitchUserEnsureService twitchUserEnsureService,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "💣 Алармагеддон 💣";
    public override string AlertDescription { get; set; } =
        "После активации, все использованные награды на этом канале, в течении 1 минуты, возращают потраченные баллы канала";
    public override Color Color { get; set; } = Color.Aqua;
    public override int Cost { get; init; } = 99999;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
    private static bool IsRedemptionActive { get; set; }

    private protected override CreateCustomRewardsRequest CreateCustomRewardsRequest
    {
        get
        {
            var value = base.CreateCustomRewardsRequest;
            value.IsUserInputRequired = true;
            value.ShouldRedemptionsSkipRequestQueue = true;
            value.MaxPerStream = 1;
            return value;
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd += OnRewardActivation;
        wsClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsCustomRewardRedemption;

        await base.StartAsync(cancellationToken);
    }

    private async Task OnRewardActivation(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        var vr = await validator
            .ForRedemption(e)
            .RequireBroadcasterUserId()
            .RequireRewardEnabled(IsRewardEnabled)
            .RequireCost(Cost)
            .ValidateWithResponseAsync(e.Payload.Event.UserName);

        if (vr.IsInvalid)
        {
            return;
        }

        var twEvent = e.Payload.Event;
        var twitchUser = TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(e)!;

        twitchUser = await twitchUserEnsureService.EnsureUserExistsAsync(twitchUser);

        // Отправляем сообщение пользователю о возврате баллов
        await api.SendAnnouncementToMainTwitchAsync(
            $"@{twEvent.UserName}, активировал @{AlertDisplayName}! Начинайте тратить баллы через 10 секунд!",
            tokenService.Token,
            AnnouncementColors.Primary,
            logger
        );

        await hubContext.Clients.All.AllRefund(twitchUser);

        await Task.Delay(TimeSpan.FromSeconds(10));

        IsRedemptionActive = true;

        await Task.Factory.StartNew(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1));

            // Отправляем сообщение пользователю о возврате баллов
            await api.SendAnnouncementToMainTwitchAsync(
                $"@{AlertDisplayName} закончился! Всем спасибо за участие!",
                tokenService.Token,
                AnnouncementColors.Primary,
                logger
            );

            IsRedemptionActive = false;
        });
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnRewardActivation;

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
            .ValidateWithResponseAsync(args.Payload.Event.UserName);

        if (vr.IsInvalid)
        {
            return;
        }

        var twEvent = args.Payload.Event;

        if (!IsRedemptionActive)
        {
            return;
        }

        try
        {
            logger.LogInformation(
                "Возврат баллов для пользователя {UserName} во время работыы супер возращения баллов",
                twEvent.UserName
            );

            // Возвращаем баллы пользователю
            await api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                TwitchExstension.ChannelId,
                twEvent.Reward.Id,
                [args.Payload.Event.Id],
                new UpdateCustomRewardRedemptionStatusRequest
                {
                    Status = CustomRewardRedemptionStatus.CANCELED,
                },
                tokenService.Token?.AccessToken
            );

            // Отправляем сообщение пользователю о возврате баллов
            await client.SendMessageToMainTwitchAsync(
                $"@{twEvent.UserName}, твои {Cost} баллов были возвращены!",
                logger
            );

            logger.LogInformation(
                "Баллы успешно возвращены пользователю {UserName}",
                twEvent.UserName
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }
}
