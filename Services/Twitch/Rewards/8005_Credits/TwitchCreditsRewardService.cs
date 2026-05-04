using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._8005_Credits;

/// <summary>
/// Сервис для обработки награды Credits на Twitch
/// </summary>
public class TwitchCreditsRewardService(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    ILogger<TwitchCreditsRewardService> logger,
    EventSubWebsocketClient wsClient
) : BackgroundService, ITwitchReward
{
    public bool IsServiceActive { get; set; } = true;
    public int Cost { get; init; } = 8005;

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

        var twEvent = args.Payload.Event;

        // Проверяем, что это награда за 8005 баллов и от нужного канала
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
                logger.LogInformation(
                    "Credits награда активирована пользователем {UserName} за {Cost} баллов",
                    twEvent.UserName,
                    twEvent.Reward.Cost
                );

                // Отправляем метод Credits в хаб
                await hubContext.Clients.All.Credits();

                logger.LogInformation(
                    "Credits эффект активирован для пользователя {UserName}",
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
