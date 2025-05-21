using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.Twitch.Rewards.TwitchClipCreator;

public class TwitchClipCreatorService(
    ITwitchClient client,
    ITwitchAPI api,
    IHostApplicationLifetime lifetime,
    TokenService tokenService,
    ILogger<TwitchClipCreatorService> logger
) : BackgroundService
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });

        return Task.CompletedTask;
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        var userName = twEvent.UserName;
        var cost = twEvent.Reward.Cost;

        if (cost == 1)
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
