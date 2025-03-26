using MARS.Server.Services.RandomMem.Entity;

namespace MARS.Server.Services.Twitch.Rewards.TwitchRandomMeme;

public class RandomMeme(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IWebHostEnvironment webHostEnvironment,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime applicationLifetime
)
{
    private readonly CancellationToken _stoppingToken = applicationLifetime.ApplicationStopping;

    public async Task RandomMemeHandler(object sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        var twEvent = args.Notification.Payload.Event;
        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            switch (twEvent.Reward.Cost)
            {
                case 9:
                {
                    var media = await GetMeme(twEvent.UserName);

                    if (media is not null)
                    {
                        await hubContext.Clients.All.Alert(
                            new MediaDto(media) { MediaInfo = media }
                        );
                    }

                    break;
                }
                case 10:
                {
                    var sound = await GetRandomSound(twEvent.UserName);

                    if (sound is not null)
                    {
                        await hubContext.Clients.All.Alert(
                            new MediaDto(sound) { MediaInfo = sound }
                        );
                    }

                    break;
                }
            }
        }
    }

    private Task<MediaInfo?> GetRandomSound(string? displayName)
    {
        var path = Path.Combine(webHostEnvironment.WebRootPath, "Alerts", "zvik");
        return GetAlert(path, displayName);
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
        var fileType = await filePath.GetFileMediaTypeAsync();
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

        var nextVideoOrder = await dbContext
            .RandomMemeOrder.OrderBy(o => o.Order)
            .FirstOrDefaultAsync(e => e.Order == 1 && e.MemeTypeId == type.Id, _stoppingToken);

        if (nextVideoOrder is null)
        {
            throw new NullReferenceException();
        }

        var maxOrder = await dbContext
            .RandomMemeOrder.AsNoTracking()
            .MaxAsync(e => e.Order, cancellationToken: _stoppingToken);

        nextVideoOrder.Order = maxOrder;

        dbContext.RandomMemeOrder.Update(nextVideoOrder);

        await dbContext
            .RandomMemeOrder.Where(e => e.Id != nextVideoOrder.Id && e.MemeTypeId != type.Id)
            .ExecuteUpdateAsync(
                e => e.SetProperty(a => a.Order, order => order.Order - 1),
                cancellationToken: _stoppingToken
            );

        // Save changes to the database
        await dbContext.SaveChangesAsync(_stoppingToken);

        return nextVideoOrder;
    }
}
