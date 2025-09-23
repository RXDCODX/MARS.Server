using MARS.Server.Services.Twitch.Management;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchRefundService;

/// <summary>
/// Сервис для возврата баллов канала при активации алерта за 160 баллов с текстом "asp"
/// </summary>
public class TwitchRefundService(
    ITwitchAPI api,
    ITwitchClient client,
    ILogger<TwitchRefundService> logger,
    EventSubWebsocketClient wsClient,
    TokenService tokenService
) : BackgroundService
{
    public bool IsServiceActive { get; set; } = true;

    private const int RefundRewardCost = 160;
    private const string RefundRewardId = "e0af123a-3987-4924-a86b-393a702d2857\r\n";
    private static readonly string[] AspVariations = ["asp", "ASP", "Asp", "асп", "Асп", "АСП"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        }

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
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
        if (!IsServiceActive)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenService.Token?.AccessToken);

        var twEvent = args.Payload.Event;

        // Проверяем, что это награда за 160 баллов и от нужного канала
        if (
            twEvent.Reward.Cost == RefundRewardCost
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
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
                        RefundRewardId,
                        [args.Payload.Event.Id],
                        new UpdateCustomRewardRedemptionStatusRequest
                        {
                            Status = CustomRewardRedemptionStatus.CANCELED,
                        },
                        tokenService.Token.AccessToken
                    );

                    // Отправляем сообщение пользователю о возврате баллов
                    await client.SendMessageToMainTwitchAsync(
                        $"@{twEvent.UserName}, твои {RefundRewardCost} баллов были возвращены!",
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
