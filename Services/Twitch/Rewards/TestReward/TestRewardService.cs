using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TestReward;

/// <summary>
/// Сервис для тестирования перехвата активации награды с ценой 88888
/// </summary>
public class TestRewardService(
    ITwitchAPI api,
    ITwitchClient client,
    ILogger<TestRewardService> logger,
    EventSubWebsocketClient wsClient,
    TokenService tokenService
) : BackgroundService, ITwitchReward
{
    public bool IsServiceActive { get; set; } = true;
    public int Cost { get; init; } = 88888;

    private static readonly string[] TestVariations =
    [
        "test",
        "TEST",
        "Test",
        "тест",
        "Тест",
        "ТЕСТ",
    ];

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

        // Проверяем, что это награда за 88888 баллов и от нужного канала
        if (
            twEvent.Reward.Cost == Cost
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            try
            {
                // Проверяем, содержит ли пользовательский ввод текст "test" в различных транскрипциях
                var userInput = twEvent.UserInput?.Trim() ?? string.Empty;

                if (
                    TestVariations.Any(variation =>
                        userInput.Contains(variation, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    logger.LogInformation(
                        "Перехват тестовой награды для пользователя {UserName} с текстом '{UserInput}'",
                        twEvent.UserName,
                        userInput
                    );

                    // Отменяем выполнение награды
                    await api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                        TwitchExstension.ChannelId,
                        twEvent.Reward.Id,
                        [args.Payload.Event.Id],
                        new UpdateCustomRewardRedemptionStatusRequest
                        {
                            Status = CustomRewardRedemptionStatus.CANCELED,
                        },
                        tokenService.Token.AccessToken
                    );

                    // Отправляем сообщение пользователю о отмене награды
                    await client.SendMessageToMainTwitchAsync(
                        $"@{twEvent.UserName}, твоя тестовая награда была отменена! Введенный текст: '{userInput}'",
                        logger
                    );

                    logger.LogInformation(
                        "Тестовая награда успешно отменена для пользователя {UserName}",
                        twEvent.UserName
                    );
                }
                else
                {
                    // Если текст не содержит "test", выполняем награду
                    await api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                        TwitchExstension.ChannelId,
                        twEvent.Reward.Id,
                        [args.Payload.Event.Id],
                        new UpdateCustomRewardRedemptionStatusRequest
                        {
                            Status = CustomRewardRedemptionStatus.FULFILLED,
                        },
                        tokenService.Token.AccessToken
                    );

                    await client.SendMessageToMainTwitchAsync(
                        $"@{twEvent.UserName}, тестовая награда выполнена! Введенный текст: '{userInput}'",
                        logger
                    );

                    logger.LogInformation(
                        "Тестовая награда выполнена для пользователя {UserName}",
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
