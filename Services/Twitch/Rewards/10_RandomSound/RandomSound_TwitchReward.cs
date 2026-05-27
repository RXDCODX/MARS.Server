using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Media;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._10_RandomSound;

public class RandomSound_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<RandomSound_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IWebHostEnvironment webHostEnvironment,
    ITwitchMediaPreparationService twitchMediaPreparationService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ITelegramBotClient telegramBotClient,
    IHostApplicationLifetime applicationLifetime,
    EventSubWebsocketClient wsClient,
    RickRollerService rickRollerService
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🔊 Random sound";
    public override string AlertDescription { get; set; } = "😂 Нажимать ради смешного момента";
    public override Color Color { get; set; } = Color.FromArgb(122, 167, 255);
    public override int Cost { get; init; } = 10;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    private readonly CancellationToken _stoppingToken = applicationLifetime.ApplicationStopping;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd += RandomSoundHandler;
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= RandomSoundHandler;
        return base.StopAsync(cancellationToken);
    }

    private async Task RandomSoundHandler(
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
                    var sound = await GetRandomSound(twEvent.UserName);

                    if (sound is not null)
                    {
                        await hubContext.Clients.All.RandomMem(
                            new MediaDto(sound) { MediaInfo = sound }
                        );
                    }
                }
            );
        }
    }

    private Task<MediaInfo?> GetRandomSound(string? displayName)
    {
        var path = Path.Combine(webHostEnvironment.WebRootPath, "Alerts", "zvik");
        return GetAlert(path, displayName);
    }

    private async Task<MediaInfo?> GetAlert(string path, string? displayName)
    {
        var mediaOrder = await GetNextVideoOrderAsync(path);
        var sourcePath = mediaOrder.FilePath;

        await SendTranscodeNotificationAsync(
            $"Начата обработка файла: {sourcePath}",
            _stoppingToken
        );

        var media = await twitchMediaPreparationService.PrepareMediaAsync(
            mediaOrder,
            displayName,
            _stoppingToken
        );

        var successCount = media is not null ? 1 : 0;
        var failedCount = media is null ? 1 : 0;
        var statusText = media is not null ? "успех" : "ошибка";

        await SendTranscodeNotificationAsync(
            $"Обработка файлов завершена\nВсего файлов: 1\nУспешно: {successCount}\nС ошибкой: {failedCount}\nПолный список:\n1. {sourcePath} [{statusText}]",
            _stoppingToken
        );

        return media;
    }

    private async Task SendTranscodeNotificationAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await telegramBotClient.SendMessage(
                TelegramExstension.Rxdcodx,
                message,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось отправить уведомление о подготовке медиа");
        }
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
