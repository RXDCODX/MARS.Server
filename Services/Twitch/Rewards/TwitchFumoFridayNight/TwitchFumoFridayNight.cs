using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchFumoFridayNight;

public class TwitchFumoFridayNight(
    ChannelRewardsService channelRewardsService,
    ILogger<TwitchFumoFridayNight> logger,
    IHostEnvironment environment,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext
) : TemporaryReward(channelRewardsService, logger, environment)
{
    private readonly string _videoPath = Path.Combine(
        environment.ContentRootPath,
        "wwwroot",
        "Alerts",
        "fumoFridayNight.mp4"
    );

    private protected override CreateCustomRewardsRequest CreateCustomRewardsRequest =>
        new()
        {
            Title = AlertDisplayName,
            Prompt = AlertDescription,
            Cost = Cost,
            IsEnabled = true,
            IsUserInputRequired = false,
            IsMaxPerStreamEnabled = false,
            IsMaxPerUserPerStreamEnabled = false,
            IsGlobalCooldownEnabled = true,
            ShouldRedemptionsSkipRequestQueue = false,
            GlobalCooldownSeconds = 180,
        };

    public override string AlertDisplayName { get; set; } = "Fumo Friday Night";
    public override string AlertDescription { get; set; } =
        "Твоя уникальная (ну почти) возможность активации Fumo Friday Night";
    public override Color Color { get; set; } = Color.Red;
    public override int Cost { get; init; } = 170;
    public override Func<DateTime, bool> IsRewardEnabled { get; set; } =
        time => time.DayOfWeek == DayOfWeek.Friday;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd -=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });

        return base.StartAsync(cancellationToken);
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;

        var cost = twEvent.Reward.Cost;
        var text = args.Payload.Event.UserInput;
        var channel = twEvent.BroadcasterUserId;

        if (
            channel.Equals(TwitchExstension.ChannelId, StringComparison.OrdinalIgnoreCase)
            && cost == Cost
        )
        {
            await Task.Run(async () =>
            {
                try
                {
                    var fileName = Path.GetFileName(_videoPath);
                    var extension = Path.GetExtension(fileName).TrimStart('.');
                    var relativePath = $"/Alerts/{fileName}";

                    var mediaInfo = new MediaInfo
                    {
                        TextInfo = new MediaTextInfo
                        {
                            Text = $"🎵 {{user.name}} активировал(а) {AlertDisplayName}! 🎵",
                            TextColor = "#FFFFFF",
                        },
                        FileInfo = new MediaFileInfo
                        {
                            Type = MediaType.Video,
                            FilePath = relativePath,
                            IsLocalFile = true,
                            FileName = fileName,
                            Extension = extension,
                        },
                        PositionInfo = new MediaPositionInfo
                        {
                            RandomCoordinates = true,
                            Height = 500,
                            Width = 500,
                            IsProportion = true,
                            IsUseOriginalWidthAndHeight = false,
                        },
                        MetaInfo = new MediaMetaInfo
                        {
                            DisplayName = twEvent.UserName,
                            Duration = 30,
                            Volume = 100,
                            Priority = MediaAlertPriority.Normal,
                        },
                        StylesInfo = new MediaStylesInfo(),
                    };

                    mediaInfo.FixAlertText(twEvent.UserName, string.Empty);

                    var mediaDto = new MediaDto(mediaInfo);
                    await hubContext.Clients.All.Alert(mediaDto);

                    logger.LogInformation(
                        "{AlertName} активирован пользователем {UserName}",
                        AlertDisplayName,
                        twEvent.UserName
                    );
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Ошибка при активации {AlertName}", AlertDisplayName);
                }
            });
        }
    }
}
