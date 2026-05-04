using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._155_MichaelTime;

/// <summary>
/// Сервис для обработки награды MichaelJackson на Twitch
/// </summary>
public class TwitchMichaelJacksonRewardService(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    ILogger<TwitchMichaelJacksonRewardService> logger,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime
) : BackgroundService, ITwitchReward
{
    public bool IsServiceActive { get; set; } = true;
    public int Cost { get; init; } = 155;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
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

        return Task.CompletedTask;
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

        // Проверяем, что это награда за 155 баллов и от нужного канала
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
                    "MichaelJackson награда активирована пользователем {UserName} за {Cost} баллов",
                    twEvent.UserName,
                    twEvent.Reward.Cost
                );

                // Отправляем метод MichaelJackson в хаб
                await hubContext.Clients.All.MichaelJackson();

                logger.LogInformation(
                    "MichaelJackson эффект активирован для пользователя {UserName}",
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
