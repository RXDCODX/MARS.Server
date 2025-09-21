using MARS.Server.Services.Twitch.Management;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchClipCreator;

/// <inheritdoc />
public class TwitchClipCreatorService(
    ITwitchClient client,
    ITwitchAPI api,
    IHostApplicationLifetime lifetime,
    TokenService tokenService,
    ILogger<TwitchClipCreatorService> logger,
    EventSubWebsocketClient wsClient
) : BackgroundService
{
    public bool IsServiceActive { get; set; }

    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        }

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;
        await base.StopAsync(cancellationToken);
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        var userName = twEvent.UserName;
        var cost = twEvent.Reward.Cost;

        if (cost == 1 && IsServiceActive)
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        var response = await api
                            .Helix.Clips.CreateClipAsync(
                                TwitchExstension.ChannelId,
                                tokenService.Token?.AccessToken
                            )
                            .ConfigureAwait(false);

                        var editUrl = response.CreatedClips[0].EditUrl;

                        await client.SendMessageToMainTwitchAsync(
                            $"@{userName}, вот твоя ссылка для редактирования - {editUrl}"
                        );
                    }
                    catch (Exception e)
                    {
                        logger.LogException(e);
                    }
                },
                _cancellationToken
            );
        }
    }
}
