using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Validation;
using MARS.Server.Services.Twitch.Management.Entitys;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._160_LegBum;

/// <summary>
/// Сервис для возврата баллов канала при активации алерта за 160 баллов с текстом "asp"
/// </summary>
public class LegBumRefundService(
    ITwitchAPI api,
    ITwitchClient client,
    ILogger<LegBumRefundService> logger,
    EventSubWebsocketClient wsClient,
    TokenService tokenService,
    LegBum_TwitchReward reward,
    ITwitchEventValidationService validator
) : BackgroundService, ITwitchReward
{
    public bool IsServiceActive { get; set; } = true;
    public int Cost { get; init; } = 160;

    private static readonly string[] AspVariations = ["asp", "асп"];

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        }

        return Task.CompletedTask;
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
            .RequireServiceActive(IsServiceActive)
            .RequireCost(Cost)
            .ValidateAsync();

        if (vr.IsInvalid)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenService.Token?.AccessToken);

        var twEvent = args.Payload.Event;

        if (reward.TwitchRewardId.HasValue)
        {
            try
            {
                // Проверяем, содержит ли пользовательский ввод текст "asp" в различных транскрипциях
                var userInput = twEvent.UserInput?.Trim() ?? string.Empty;

                if (
                    AspVariations.Any(variation =>
                        userInput.Contains(variation, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    logger.LogInformation(
                        "Возврат баллов для пользователя {UserName} за награду с текстом '{UserInput}'",
                        twEvent.UserName,
                        userInput
                    );

                    // Возвращаем баллы пользователю
                    await api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                        TwitchExstension.ChannelId,
                        reward.TwitchRewardId.Value.ToString(),
                        [args.Payload.Event.Id],
                        new UpdateCustomRewardRedemptionStatusRequest
                        {
                            Status = CustomRewardRedemptionStatus.CANCELED,
                        },
                        tokenService.Token.AccessToken
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
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }
    }
}
