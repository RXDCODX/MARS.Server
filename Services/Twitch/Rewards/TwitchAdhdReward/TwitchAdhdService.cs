using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchAdhdReward;

/// <summary>
/// Сервис для обработки награды ADHD на Twitch
/// </summary>
public class TwitchAdhdService(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    ILogger<TwitchAdhdService> logger,
    EventSubWebsocketClient wsClient
) : BackgroundService
{
    public bool IsServiceActive { get; set; } = true;

    private const int AdhdRewardCost = 2002;
    private const int AdhdDurationSeconds = 60;

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

        // Проверяем, что это награда за 2002 поинта и от нужного канала
        if (
            twEvent.Reward.Cost == AdhdRewardCost
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            try
            {
                logger.LogInformation(
                    "ADHD награда активирована пользователем {UserName} за {Cost} поинтов",
                    twEvent.UserName,
                    twEvent.Reward.Cost
                );

                // Отправляем метод Adhd в хаб на 60 секунд
                await hubContext.Clients.All.Adhd(AdhdDurationSeconds);

                logger.LogInformation(
                    "ADHD эффект активирован на {Duration} секунд для пользователя {UserName}",
                    AdhdDurationSeconds,
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
