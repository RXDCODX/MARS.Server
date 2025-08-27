using MARS.Server.Services.ServiceManager;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchAdhdReward;

/// <summary>
/// Сервис для обработки награды ADHD на Twitch
/// </summary>
public class TwitchAdhdService(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IHostApplicationLifetime applicationLifetime,
    ILogger<TwitchAdhdService> logger,
    EventSubWebsocketClient wsClient
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "twitchadhd";
    public override string DisplayName => "Twitch ADHD";
    public override string Description => "Сервис для обработки награды ADHD на Twitch";
    public override bool IsServiceActive { get; set; } = true;

    private const int AdhdRewardCost = 2002;
    private const int AdhdDurationSeconds = 60;

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsServiceActive)
        {
            applicationLifetime.ApplicationStarted.Register(() =>
            {
                wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                    OnChannelPointsCustomRewardRedemption;
            });
        }

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;
        return base.StopAsync(cancellationToken);
    }

    private async Task OnChannelPointsCustomRewardRedemption(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        if (!IsServiceActive)
        {
            return;
        }

        var twEvent = args.Notification.Payload.Event;

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
