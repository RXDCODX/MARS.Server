using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
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
    ITwitchClient client
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "БЕСКОНЕЧНЫЕ НАГРАДЫ";
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
        var twEvent = e.Payload.Event;

        if (
            twEvent.Reward.Cost == Cost
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && IsRewardEnabled()
        )
        {
            // Отправляем сообщение пользователю о возврате баллов
            await client.SendMessageToMainTwitchAsync(
                $"@{twEvent.UserName}, активировал возращалку! Начинайте тратить баллы!",
                logger
            );

            await hubContext.Clients.All.AllRefund(
                TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(e)!
            );

            await Task.Delay(TimeSpan.FromSeconds(5));

            IsRedemptionActive = true;

            await Task.Factory.StartNew(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

                // Отправляем сообщение пользователю о возврате баллов
                await client.SendMessageToMainTwitchAsync(
                    $"@{twEvent.UserName}, время работы возращалки закончилось! Всем спасибо за участие!",
                    logger
                );

                IsRedemptionActive = false;
            });
        }
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
        var twEvent = args.Payload.Event;

        if (
            twEvent.Reward.Cost == Cost
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && IsRedemptionActive
            && IsRewardEnabled()
        )
        {
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
}
