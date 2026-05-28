using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MARS.Server.Services.Twitch.Media;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Rewards._11_RandomMemReward;

public class RandomMem_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<RandomMem_TwitchReward> logger,
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
        var sourcePath = mediaOrder.FilePath;
        var transcodeReports = new List<string>();

        var media = await twitchMediaPreparationService.PrepareMediaAsync(
            mediaOrder,
            displayName,
            _stoppingToken,
            report =>
            {
                transcodeReports.Add(report);
                return Task.CompletedTask;
            }
        );

        if (transcodeReports.Count > 0)
        {
            await SendTranscodeNotificationAsync(
                BuildSingleFileSummary(sourcePath, transcodeReports),
                _stoppingToken
            );
        }

        return media;
    }

    private static string BuildSingleFileSummary(string sourcePath, IReadOnlyList<string> transcodeReports)
    {
        var result = new StringBuilder();

        result.AppendLine("Обработка файлов завершена");
        result.AppendLine("Всего файлов: 1");
        result.AppendLine($"Требовали конвертацию: {transcodeReports.Count}");
        result.AppendLine("Полный список:");
        result.AppendLine($"1. {sourcePath}");

        foreach (var report in transcodeReports)
        {
            result.AppendLine();
            result.AppendLine(report);
        }

        return result.ToString().TrimEnd();
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
