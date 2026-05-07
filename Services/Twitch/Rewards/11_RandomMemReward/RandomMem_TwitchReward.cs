using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._11_RandomMemReward;

public class RandomMem_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<RandomMem_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IWebHostEnvironment webHostEnvironment,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime applicationLifetime,
    EventSubWebsocketClient wsClient,
    RickRollerService rickRollerService
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "😂 Random Mem";

    public override string AlertDescription { get; set; } = "🤣 Рандомный мем на экране";

    public override Color Color { get; set; } = Color.FromArgb(243, 255, 0);

    public override int Cost { get; init; } = 11;

    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    private readonly CancellationToken _stoppingToken = applicationLifetime.ApplicationStopping;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd += RandomMemeHandler;
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= RandomMemeHandler;
        return base.StopAsync(cancellationToken);
    }

    private async Task RandomMemeHandler(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;

        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
            && twEvent.Reward.Cost == Cost
        )
        {
            await rickRollerService.TryRickRollAsync(
                TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                async () =>
                {
                    var media = await GetMeme(twEvent.UserName);

                    if (media is not null)
                    {
                        await hubContext.Clients.All.RandomMem(
                            new MediaDto(media) { MediaInfo = media }
                        );
                    }
                }
            );
        }
    }

    private Task<MediaInfo?> GetMeme(string? displayName)
    {
        var path = Path.Combine(webHostEnvironment.WebRootPath, "Alerts", "random_meme");
        return GetAlert(path, displayName);
    }

    private async Task<MediaInfo?> GetAlert(string path, string? displayName)
    {
        var mediaOrder = await GetNextVideoOrderAsync(path);
        var filePath = mediaOrder.FilePath;

        var exst = Path.GetExtension(filePath);
        var fileType = await exst.GetFileMediaTypeAsync();
        var shortPath = filePath[
            (filePath.IndexOf("wwwroot", StringComparison.Ordinal) + "wwwroot".Length)..
        ];

        var mediaInfo = new MediaInfo
        {
            FileInfo = new MediaFileInfo
            {
                Extension = exst,
                Type = fileType,
                FileName = Path.GetFileName(filePath),
                FilePath = shortPath,
            },
            MetaInfo = new MediaMetaInfo
            {
                DisplayName = displayName ?? string.Empty,
                IsLooped = false,
            },
            PositionInfo = new MediaPositionInfo
            {
                Height = 400,
                Width = 400,
                IsProportion = true,
                IsResizeRequires = true,
            },
            StylesInfo = new MediaStylesInfo { IsBorder = false },
            TextInfo = new MediaTextInfo(),
        };

        return mediaInfo;
    }

    public async Task<MemeOrder> GetNextVideoOrderAsync(string path)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_stoppingToken);

        var type = dbContext
            .RandomMemeType.AsNoTracking()
            .AsEnumerable()
            .First(e => path.Contains(e.FolderPath, StringComparison.OrdinalIgnoreCase));

        var nextVideoOrder =
            await dbContext
                .RandomMemeOrder.OrderBy(o => o.Order)
                .FirstOrDefaultAsync(e => e.MemeTypeId == type.Id, _stoppingToken)
            ?? throw new NullReferenceException();
        var maxOrder = await dbContext
            .RandomMemeOrder.AsNoTracking()
            .MaxAsync(e => e.Order, _stoppingToken);

        nextVideoOrder.Order = maxOrder;

        dbContext.RandomMemeOrder.Update(nextVideoOrder);

        await dbContext
            .RandomMemeOrder.Where(e => e.Id != nextVideoOrder.Id && e.MemeTypeId == type.Id)
            .ExecuteUpdateAsync(
                e => e.SetProperty(a => a.Order, order => order.Order - 1),
                _stoppingToken
            );

        await dbContext.SaveChangesAsync(_stoppingToken);

        return nextVideoOrder;
    }
}
