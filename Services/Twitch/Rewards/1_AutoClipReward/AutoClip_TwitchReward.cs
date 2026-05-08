using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._1_AutoClipReward;

/// <inheritdoc />
public class AutoClip_TwitchReward(
    ITwitchClient client,
    ITwitchAPI api,
    IHostApplicationLifetime lifetime,
    IHostEnvironment hostEnvironment,
    TokenService tokenService,
    ILogger<AutoClip_TwitchReward> logger,
    EventSubWebsocketClient wsClient,
    ChannelRewardsService channelRewardsService
) : TemporaryReward(channelRewardsService, logger, hostEnvironment)
{
    public override string AlertDisplayName { get; set; } = "🎬 Клипнуть!";
    public override string AlertDescription { get; set; } =
        "🎥 Сделать автоклип последних 30 секунд стрима!";
    public override Color Color { get; set; }
    public override int Cost { get; init; } = 1;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    public override Task StartAsync(CancellationToken stoppingToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd +=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;

        return base.StartAsync(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;

        return base.StopAsync(cancellationToken);
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;
        var userName = twEvent.UserName;
        var cost = twEvent.Reward.Cost;

        if (cost == Cost && IsRewardEnabled())
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
