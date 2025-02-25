using Hangfire;
using MARS.Server.Services.RandomMem.Entity;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.Rewards.TwitchRandomMeme;

public class RandomMeme
{
    private readonly ITwitchClient _client;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly ILogger<RandomMeme> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    private readonly IWebHostEnvironment _webHostEnvironment;

    public RandomMeme(
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IWebHostEnvironment webHostEnvironment,
        ITwitchClient client,
        ILogger<RandomMeme> logger,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _hubContext = hubContext;
        _webHostEnvironment = webHostEnvironment;
        _client = client;
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

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
                        BackgroundJob.Enqueue(
                            () =>
                                _hubContext.Clients.All.Alert(
                                    new MediaDto(media) { MediaInfo = media }
                                )
                        );
                    }

                    break;
                }
                case 10:
                {
                    var sound = await GetRandomSound(twEvent.UserName);

                    if (sound is not null)
                    {
                        BackgroundJob.Enqueue(
                            () =>
                                _hubContext.Clients.All.Alert(
                                    new MediaDto(sound) { MediaInfo = sound }
                                )
                        );
                    }

                    break;
                }
                case 3:
                {
                    var sound = await GetHigemSound(twEvent.UserName);

                    if (sound is not null)
                    {
                        BackgroundJob.Enqueue(
                            () =>
                                _hubContext.Clients.All.Alert(
                                    new MediaDto(sound) { MediaInfo = sound }
                                )
                        );
                    }

                    break;
                }
            }
        }
    }

    private Task<MediaInfo?> GetRandomSound(string? displayName)
    {
        var path = Path.Combine(_webHostEnvironment.WebRootPath, "Alerts", "zvik");
        return GetAlert(path, displayName);
    }

    private Task<MediaInfo?> GetHigemSound(string? displayName)
    {
        var path = Path.Combine(_webHostEnvironment.WebRootPath, "Alerts", "zvik", "higem");
        return GetAlert(path, displayName, true);
    }

    private Task<MediaInfo?> GetMeme(string? displayName)
    {
        var path = Path.Combine(_webHostEnvironment.WebRootPath, "Alerts", "random_meme");
        return GetAlert(path, displayName, true);
    }

    private async Task<MediaInfo?> GetAlert(
        string path,
        string? displayName,
        bool isSearchInDirs = false
    )
    {
        var files = Directory.GetFiles(
            path,
            "*.*",
            isSearchInDirs ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
        );

        if (files.Length > 0)
        {
            Random.Shared.Shuffle(files);
            var filePath = files[0];

            if (string.IsNullOrWhiteSpace(filePath))
            {
                await _client.SendMessageToPyrokxnezxzAsync("Не могу найти мем", _logger);
                return null;
            }

            var exst = Path.GetExtension(filePath);
            var fileType = await exst.GetFileMediaTypeAsync();

            var shortPath = filePath.Substring(
                filePath.IndexOf("wwwroot", StringComparison.Ordinal) + "wwwroot".Length
            );

            File.SetLastWriteTime(filePath, DateTime.Now);

            var mediaInfo = new MediaInfo
            {
                FileInfo = new MediaFileInfo
                {
                    Extension = exst,
                    Type = fileType,
                    FileName = Path.GetFileName(filePath),
                    LocalFilePath = shortPath,
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
        return null;
    }

    public async Task<VideoOrder?> GetNextVideoOrderAsync(
        string path,
        CancellationToken stoppingToken
    )
    {
        await using AppDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(
            stoppingToken
        );

        // Get the first video order from the database
        var nextVideoOrder = await dbContext
            .RandomMemeOrder.OrderBy(o => o.Order)
            .FirstOrDefaultAsync(stoppingToken);

        if (nextVideoOrder != null)
        {
            // Get all video orders except the one being moved
            var otherOrders = await dbContext
                .RandomMemeOrder.Where(e => e.Id != nextVideoOrder.Id)
                .ExecuteUpdateAsync(
                    e => e.SetProperty(a => a.Order, order => order.Order - 1),
                    cancellationToken: stoppingToken
                );

            // Save changes to the database
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        return nextVideoOrder;
    }
}
