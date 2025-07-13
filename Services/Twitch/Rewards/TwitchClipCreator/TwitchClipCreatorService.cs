using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.Twitch.Rewards.TwitchClipCreator;

/// <inheritdoc />
public class TwitchClipCreatorService(
    ITwitchClient client,
    ITwitchAPI api,
    IHostApplicationLifetime lifetime,
    TokenService tokenService,
    ILogger<TwitchClipCreatorService> logger
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "twitchclipcreator";
    public override string DisplayName => "Twitch Clip Creator";
    public override string Description => "Создание клипов Twitch";
    public override bool IsServiceActive { get; set; }

    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            lifetime.ApplicationStarted.Register(() =>
            {
                EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd += WsClientOnChannelPointsCustomRewardRedemptionAdd;
            });
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;
        return base.StopAsync(cancellationToken);
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
