using MARS.Server.ApplicationState;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._170_FumoFridayNightReward;

public class FumoFridayNight_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<FumoFridayNight_TwitchReward> logger,
    IHostEnvironment environment,
    IDbContextFactory<AppDbContext> dbContextFactory,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    RickRollerService rickRoller
) : TemporaryReward(channelRewardsService, logger, environment)
{
    private const string VideoPathEnvironmentVariable = "TWITCH_FUMO_FRIDAY_NIGHT_VIDEO_PATH";
    private const string DefaultVideoRelativePath = "wwwroot/Alerts/fumoFridayNight.webm";

    private readonly ILogger _logger = logger;
    private readonly string _contentRootPath = environment.ContentRootPath;

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

    public override string AlertDisplayName { get; set; } = "🧸 Fumo Friday Night";
    public override string AlertDescription { get; set; } =
        "🎪 Твоя уникальная (ну почти) возможность активации Fumo Friday Night";
    public override Color Color { get; set; } = Color.Red;
    public override int Cost { get; init; } = 170;
    public override Func<bool> IsRewardEnabled { get; set; } =
        () => DateTime.Now.DayOfWeek == DayOfWeek.Friday;

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
                    await rickRoller.TryRickRollAsync(
                        TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                        async () =>
                        {
                            var videoPath = await ResolveVideoPathAsync();
                            var fileName = Path.GetFileName(videoPath);
                            var extension = Path.GetExtension(fileName).TrimStart('.');
                            var relativePath = $"/Alerts/{fileName}";

                            var mediaInfo = new MediaInfo
                            {
                                TextInfo = new MediaTextInfo
                                {
                                    Text =
                                        $"🎵 {{user.name}} активировал(а) {AlertDisplayName}! 🎵",
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

                            _logger.LogInformation(
                                "{AlertName} активирован пользователем {UserName}",
                                AlertDisplayName,
                                twEvent.UserName
                            );
                        }
                    );
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Ошибка при активации {AlertName}", AlertDisplayName);
                }
            });
        }
    }

    private async Task<string> ResolveVideoPathAsync()
    {
        var result = Path.Combine(_contentRootPath, DefaultVideoRelativePath);

        var environmentPath = Environment.GetEnvironmentVariable(VideoPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            result = NormalizeVideoPath(environmentPath);
        }
        else
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var state = await dbContext
                .RootState.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Name == RootStateKeys.TwitchFumoFridayNightVideoPath);

            if (state is not null && !string.IsNullOrWhiteSpace(state.Value))
            {
                result = NormalizeVideoPath(state.Value);
            }
        }

        return result;
    }

    private string NormalizeVideoPath(string path)
    {
        var result = path;

        if (!Path.IsPathRooted(path))
        {
            result = Path.Combine(_contentRootPath, path);
        }

        return result;
    }
}
