using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.BooruAutoPost.Entities;
using MARS.Server.Services.BooruShared;
using MARS.Server.Services.BooruShared.Entities;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using MARS.Server.Services.Twitch.Rewards._27_RandomArt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.BooruAutoPost;

public class BooruAutoPostService(
    ILogger<BooruAutoPostService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    DanbooruRandomPostService danbooruService,
    Rule34RandomPostService rule34Service,
    IBooruDiscordPoster discordPoster,
    IBooruTelegramPoster telegramPoster,
    IDiscordGatewayService discordGatewayService,
    IDeduplicationService deduplicationService,
    ITelegramBotClient telegramBotClient,
    IOptions<TelegramConfiguration> telegramConfig,
    IHttpClientFactory httpClientFactory
) : BackgroundService, IBooruAutoPostService
{
    private const int MaxDedupRetries = 5;
    private const int SslRetryBaseDelaySeconds = 5;
    private const int SslRetryMaxDelaySeconds = 300;
    private const int MaxTelegramScheduledMessages = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var isFirstUse = true;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (isFirstUse || (!isFirstUse && await timer.WaitForNextTickAsync(stoppingToken)))
        {
            try
            {
                await ProcessScheduledPostsAsync(stoppingToken);
                isFirstUse = false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в цикле BooruAutoPostService");
            }
        }
    }

    private async Task ProcessScheduledPostsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        await RecoverPastDuePostsAsync(dbContext, now, cancellationToken);

        await ExecuteDueDiscordPostsAsync(dbContext, now, cancellationToken);

        await PlanFuturePostsAsync(dbContext, now, cancellationToken);
    }

    private async Task RecoverPastDuePostsAsync(
        AppDbContext dbContext,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var pastDuePosts = await dbContext
            .BooruScheduledPosts.Include(p => p.Config)
            .Where(p =>
                p.Status == ScheduledPostStatus.Pending
                && p.ScheduledAtUtc < now
                && p.Config.TargetPlatform == TargetPlatform.Discord
            )
            .ToListAsync(cancellationToken);

        foreach (var post in pastDuePosts)
        {
            var config = post.Config;
            if (config is null || !config.IsEnabled || string.IsNullOrWhiteSpace(config.CronExpression))
            {
                continue;
            }

            try
            {
                var cron = CronExpression.Parse(config.CronExpression);
                var nextOccurrence = cron.GetNextOccurrence(now);

                if (nextOccurrence.HasValue)
                {
                    logger.LogInformation(
                        "Перенос просроченного поста {PostId} с {OldDate} на {NewDate} для конфига {ConfigId}",
                        post.Id,
                        post.ScheduledAtUtc,
                        nextOccurrence.Value,
                        config.Id
                    );
                    post.ScheduledAtUtc = nextOccurrence.Value;
                }
            }
            catch (CronFormatException ex)
            {
                logger.LogWarning(
                    ex,
                    "Некорректное CRON выражение '{Cron}' при восстановлении поста {PostId}",
                    config.CronExpression,
                    post.Id
                );
            }
        }

        if (pastDuePosts.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ExecuteDueDiscordPostsAsync(
        AppDbContext dbContext,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var duePosts = await dbContext
            .BooruScheduledPosts.AsNoTracking()
            .Include(p => p.Config)
            .Where(p =>
                p.Status == ScheduledPostStatus.Pending
                && p.ScheduledAtUtc <= now
                && p.Config.TargetPlatform == TargetPlatform.Discord
            )
            .OrderBy(p => p.ScheduledAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var scheduledPost in duePosts)
        {
            var config = scheduledPost.Config;
            if (config is null || !config.IsEnabled)
            {
                scheduledPost.Status = ScheduledPostStatus.Cancelled;
                continue;
            }

            try
            {
                await PostImageAsync(config, cancellationToken);

                var entity = await dbContext.BooruScheduledPosts.FindAsync(
                    [scheduledPost.Id],
                    cancellationToken
                );
                if (entity is not null)
                {
                    entity.Status = ScheduledPostStatus.Posted;
                    entity.PostedAtUtc = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await using var updateContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );
                var configEntity = await updateContext.BooruAutoPostConfigs.FindAsync(
                    [config.Id],
                    cancellationToken
                );
                if (configEntity is not null)
                {
                    configEntity.LastExecutedAtUtc = DateTime.UtcNow;
                    await updateContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await HandleScheduledPostErrorAsync(
                    scheduledPost,
                    config,
                    ex,
                    cancellationToken
                );
            }
        }
    }

    private async Task HandleScheduledPostErrorAsync(
        BooruScheduledPost scheduledPost,
        BooruAutoPostConfig config,
        Exception ex,
        CancellationToken cancellationToken
    )
    {
        var isSslError =
            ex is HttpRequestException
            && ex.Message.Contains(
                "SSL connection could not be established",
                StringComparison.OrdinalIgnoreCase
            );

        if (isSslError)
        {
            logger.LogWarning(
                ex,
                "SSL ошибка при отправке поста {PostId}, повторные попытки...",
                scheduledPost.Id
            );

            var sslRetryAttempt = 0;
            var sslRecovered = false;

            while (!sslRecovered)
            {
                sslRetryAttempt++;
                var delaySeconds = Math.Min(
                    SslRetryBaseDelaySeconds * (int)Math.Pow(2, sslRetryAttempt - 1),
                    SslRetryMaxDelaySeconds
                );

                logger.LogInformation(
                    "Повторная попытка SSL {Attempt} для поста {PostId} через {Delay}с",
                    sslRetryAttempt,
                    scheduledPost.Id,
                    delaySeconds
                );

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);

                try
                {
                    await PostImageAsync(config, cancellationToken);

                    await using var successCtx =
                        await dbContextFactory.CreateDbContextAsync(cancellationToken);
                    var successEntity = await successCtx.BooruScheduledPosts.FindAsync(
                        [scheduledPost.Id],
                        cancellationToken
                    );
                    if (successEntity is not null)
                    {
                        successEntity.Status = ScheduledPostStatus.Posted;
                        successEntity.PostedAtUtc = DateTime.UtcNow;
                        await successCtx.SaveChangesAsync(cancellationToken);
                    }

                    await using var updateCtx = await dbContextFactory.CreateDbContextAsync(
                        cancellationToken
                    );
                    var cfgEntity = await updateCtx.BooruAutoPostConfigs.FindAsync(
                        [config.Id],
                        cancellationToken
                    );
                    if (cfgEntity is not null)
                    {
                        cfgEntity.LastExecutedAtUtc = DateTime.UtcNow;
                        await updateCtx.SaveChangesAsync(cancellationToken);
                    }

                    sslRecovered = true;
                    logger.LogInformation(
                        "SSL восстановлена после {Attempts} попыток для поста {PostId}",
                        sslRetryAttempt,
                        scheduledPost.Id
                    );
                }
                catch (Exception retryEx)
                {
                    var stillSsl =
                        retryEx is HttpRequestException
                        && retryEx.Message.Contains(
                            "SSL connection could not be established",
                            StringComparison.OrdinalIgnoreCase
                        );

                    if (stillSsl)
                    {
                        logger.LogWarning(
                            retryEx,
                            "SSL ошибка сохраняется, попытка {Attempt}",
                            sslRetryAttempt
                        );
                    }
                    else
                    {
                        logger.LogError(
                            retryEx,
                            "Не-SSL ошибка при повторной попытке для поста {PostId}",
                            scheduledPost.Id
                        );

                        await MarkPostAsFailedAsync(
                            scheduledPost.Id,
                            retryEx.Message,
                            cancellationToken
                        );
                        await NotifyAdminsAboutErrorAsync(
                            scheduledPost.Id,
                            config,
                            retryEx,
                            cancellationToken
                        );

                        sslRecovered = true;
                    }
                }
            }
        }
        else
        {
            logger.LogError(
                ex,
                "Ошибка отправки изображения для поста {PostId}",
                scheduledPost.Id
            );

            await MarkPostAsFailedAsync(scheduledPost.Id, ex.Message, cancellationToken);
            await NotifyAdminsAboutErrorAsync(scheduledPost.Id, config, ex, cancellationToken);
        }
    }

    private async Task MarkPostAsFailedAsync(
        Guid postId,
        string errorMessage,
        CancellationToken cancellationToken
    )
    {
        await using var errorContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var errorEntity = await errorContext.BooruScheduledPosts.FindAsync(
            [postId],
            cancellationToken
        );
        if (errorEntity is not null)
        {
            errorEntity.Status = ScheduledPostStatus.Failed;
            errorEntity.ErrorMessage = errorMessage;
            await errorContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task PlanFuturePostsAsync(
        AppDbContext dbContext,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var enabledConfigs = await dbContext
            .BooruAutoPostConfigs.AsNoTracking()
            .Where(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.CronExpression))
            .ToListAsync(cancellationToken);

        var telegramScheduledCache =
            new Dictionary<long, OperationResult<List<TelegramScheduledMessageInfo>>>();

        foreach (var config in enabledConfigs)
        {
            try
            {
                var cron = CronExpression.Parse(config.CronExpression);
                var horizonEnd = now.AddDays(config.PlanningHorizonDays);
                var scheduledTimes = GetCronOccurrences(cron, now, horizonEnd);

                if (config.TargetPlatform == TargetPlatform.Telegram)
                {
                    await PlanTelegramPostsAsync(
                        config,
                        scheduledTimes,
                        telegramScheduledCache,
                        cancellationToken
                    );
                }
                else
                {
                    var hasUpcomingPosts = await dbContext
                        .BooruScheduledPosts.AsNoTracking()
                        .AnyAsync(
                            p =>
                                p.ConfigId == config.Id
                                && p.Status == ScheduledPostStatus.Pending,
                            cancellationToken
                        );

                    if (!hasUpcomingPosts)
                    {
                        var discordPosts = scheduledTimes
                            .Select(time => new BooruScheduledPost
                            {
                                ConfigId = config.Id,
                                Source = config.Source,
                                ScheduledAtUtc = time,
                                Status = ScheduledPostStatus.Pending,
                                CreatedAtUtc = now,
                            })
                            .ToList();

                        dbContext.BooruScheduledPosts.AddRange(discordPosts);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            catch (CronFormatException ex)
            {
                logger.LogWarning(
                    ex,
                    "Некорректное CRON выражение '{Cron}' для конфига {ConfigId}",
                    config.CronExpression,
                    config.Id
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ошибка генерации расписания для конфига {ConfigId}",
                    config.Id
                );
            }
        }
    }

    private async Task PlanTelegramPostsAsync(
        BooruAutoPostConfig config,
        List<DateTime> scheduledTimes,
        Dictionary<long, OperationResult<List<TelegramScheduledMessageInfo>>> scheduledCache,
        CancellationToken cancellationToken
    )
    {
        var channelId = config.TelegramChannelId ?? 0;

        if (channelId != 0)
        {
            if (!scheduledCache.TryGetValue(channelId, out var channelMessages))
            {
                channelMessages = await telegramPoster.GetScheduledMessagesAsync(
                    channelId,
                    cancellationToken
                );
                scheduledCache[channelId] = channelMessages;
            }

            if (channelMessages.Success)
            {
                var staleMessages = await RemoveStaleTelegramMessagesAsync(
                    config,
                    channelId,
                    scheduledTimes,
                    channelMessages.Data,
                    cancellationToken
                );

                var missingTimes = TelegramScheduleMatcher.FindMissingOccurrences(
                    channelMessages.Data,
                    scheduledTimes
                );

                if (missingTimes.Count > 0)
                {
                    var maxNewPosts =
                        MaxTelegramScheduledMessages
                        - (channelMessages.Data.Count - staleMessages.Count);
                    var timesToSchedule =
                        maxNewPosts > 0 ? missingTimes.Take(maxNewPosts).ToList() : [];

                    if (timesToSchedule.Count > 0)
                    {
                        var scheduleResult = await ScheduleTelegramPostsAsync(
                            config,
                            timesToSchedule,
                            cancellationToken
                        );

                        logger.LogInformation(
                            "Конфиг {ConfigId}: запланировано {Scheduled} из {Planned} недостающих постов в Telegram",
                            config.Id,
                            scheduleResult.Data.Count,
                            timesToSchedule.Count
                        );
                    }
                    else
                    {
                        logger.LogWarning(
                            "Достигнут лимит отложенных сообщений Telegram для канала {ChannelId}, планирование пропущено",
                            channelId
                        );
                    }
                }
                else
                {
                    logger.LogDebug(
                        "Для конфига {ConfigId} все планируемые времена уже покрыты отложенными сообщениями в Telegram",
                        config.Id
                    );
                }
            }
            else
            {
                logger.LogWarning(
                    "Не удалось получить отложенные сообщения канала {ChannelId}, планирование пропущено: {Error}",
                    channelId,
                    channelMessages.Message
                );
            }
        }
    }

    private async Task<List<TelegramScheduledMessageInfo>> RemoveStaleTelegramMessagesAsync(
        BooruAutoPostConfig config,
        long channelId,
        List<DateTime> scheduledTimes,
        List<TelegramScheduledMessageInfo> channelMessages,
        CancellationToken cancellationToken
    )
    {
        var staleMessages = TelegramScheduleMatcher.FindUnmatchedMessages(
            channelMessages,
            scheduledTimes
        );

        if (staleMessages.Count > 0)
        {
            var deleteResult = await telegramPoster.DeleteScheduledMessagesAsync(
                channelId,
                staleMessages.Select(m => m.MessageId).ToList(),
                cancellationToken
            );

            if (deleteResult.Success)
            {
                logger.LogInformation(
                    "Для конфига {ConfigId} перенесено {Count} отложенных сообщений Telegram за горизонт планирования",
                    config.Id,
                    staleMessages.Count
                );
            }
            else
            {
                logger.LogWarning(
                    "Не удалось перенести отложенные сообщения Telegram для канала {ChannelId}: {Error}",
                    channelId,
                    deleteResult.Message
                );
            }
        }

        return staleMessages;
    }

    private static List<DateTime> GetCronOccurrences(
        CronExpression cron,
        DateTime now,
        DateTime horizonEnd
    )
    {
        var result = new List<DateTime>();
        var nextOccurrence = cron.GetNextOccurrence(now.AddMinutes(-1));

        while (nextOccurrence.HasValue && nextOccurrence.Value <= horizonEnd)
        {
            result.Add(nextOccurrence.Value);
            nextOccurrence = cron.GetNextOccurrence(nextOccurrence.Value);
        }

        return result;
    }

    private async Task PostImageAsync(
        BooruAutoPostConfig config,
        CancellationToken cancellationToken
    )
    {
        var channelId =
            config.TargetPlatform == TargetPlatform.Telegram
                ? (ulong)Math.Abs(config.TelegramChannelId ?? 0)
                : config.DiscordChannelId;

        var sourceName = config.Source.ToString();

        for (var attempt = 0; attempt <= MaxDedupRetries; attempt++)
        {
            (byte[] fileBytes, string fileName, int postId, Dictionary<string, string?> templateVars)?
                postData;

            if (config.Source == BooruSource.Danbooru)
            {
                postData = await FetchDanbooruPostAsync(config, cancellationToken);
            }
            else
            {
                postData = await FetchRule34PostAsync(config, cancellationToken);
            }

            if (postData is null)
            {
                return;
            }

            if (
                await deduplicationService.IsAlreadyPostedAsync(
                    sourceName,
                    postData.Value.postId,
                    channelId,
                    cancellationToken
                )
            )
            {
                if (config.SpecificPostId.HasValue)
                {
                    logger.LogInformation(
                        "Изображение {PostId} уже отправлено в канал {ChannelId}",
                        postData.Value.postId,
                        channelId
                    );
                    return;
                }

                logger.LogInformation(
                    "Иизображение {PostId} уже отправлено в канал {ChannelId}, попытка {Attempt}/{Max}",
                    postData.Value.postId,
                    channelId,
                    attempt + 1,
                    MaxDedupRetries
                );
                continue;
            }

            var resolvedMessage = BooruMessageTemplateResolver.Resolve(
                config.Message,
                postData.Value.templateVars
            );

            OperationResult sendResult;

            if (config.TargetPlatform == TargetPlatform.Telegram)
            {
                sendResult = await telegramPoster.PostAsync(
                    config.TelegramChannelId!.Value,
                    postData.Value.fileBytes,
                    postData.Value.fileName,
                    resolvedMessage,
                    config.TelegramParseMode,
                    cancellationToken
                );
            }
            else
            {
                sendResult = await discordPoster.PostAsync(
                    config.DiscordChannelId,
                    postData.Value.fileBytes,
                    postData.Value.fileName,
                    string.IsNullOrWhiteSpace(resolvedMessage) ? null : resolvedMessage,
                    cancellationToken
                );
            }

            if (sendResult.Success)
            {
                await deduplicationService.RecordPostAsync(
                    sourceName,
                    postData.Value.postId,
                    channelId,
                    cancellationToken
                );
            }
            else
            {
                logger.LogWarning(
                    "Не удалось отправить изображение в {Platform} канал {ChannelId}: {Error}",
                    config.TargetPlatform,
                    channelId,
                    sendResult.Message
                );
            }

            return;
        }

        logger.LogWarning(
            "Все {Max} попыток дедупликации исчерпаны для конфига {ConfigId}",
            MaxDedupRetries,
            config.Id
        );
    }

    private async Task<(
        byte[] fileBytes,
        string fileName,
        int postId,
        Dictionary<string, string?> templateVars
    )?> FetchDanbooruPostAsync(BooruAutoPostConfig config, CancellationToken cancellationToken)
    {
        DanbooruPost? post;

        if (config.SpecificPostId.HasValue)
        {
            post = await danbooruService.GetPostByIdAsync(config.SpecificPostId.Value);
        }
        else
        {
            var posts = await danbooruService.GetRandomPostAsync(config.Tags, 1);
            post = posts is { Length: > 0 } ? posts[0] : null;
        }

        if (post is null)
        {
            logger.LogWarning(
                "Не найдено постов по тегам '{Tags}' для конфига {ConfigId}",
                config.Tags,
                config.Id
            );
            return null;
        }

        var fileUrl = post.FileUrl ?? post.LargeFileUrl;
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            logger.LogWarning(
                "Пост {PostId} не имеет URL файла для конфига {ConfigId}",
                post.Id,
                config.Id
            );
            return null;
        }

        var (fileBytes, fileName) = await danbooruService.DownloadFileBytesAsync(
            fileUrl,
            cancellationToken
        );

        var templateVars = new Dictionary<string, string?>
        {
            { "tags", post.TagStringGeneral },
            { "character", post.TagStringCharacter },
            { "artist", post.TagStringArtist },
            { "copyright", post.TagStringCopyright },
            { "id", post.Id.ToString() },
            { "rating", post.Rating },
            { "score", post.Score?.ToString() },
            { "source", post.Source },
            { "width", null },
            { "height", null },
        };

        return (fileBytes, fileName, post.Id, templateVars);
    }

    private async Task<(
        byte[] fileBytes,
        string fileName,
        int postId,
        Dictionary<string, string?> templateVars
    )?> FetchRule34PostAsync(BooruAutoPostConfig config, CancellationToken cancellationToken)
    {
        Rule34Post? post;

        if (config.SpecificPostId.HasValue)
        {
            var posts = await rule34Service.GetRandomPostAsync(
                $"id:{config.SpecificPostId.Value}",
                1
            );
            post = posts is { Length: > 0 } ? posts[0] : null;
        }
        else
        {
            var posts = await rule34Service.GetRandomPostAsync(config.Tags, 1);
            post = posts is { Length: > 0 } ? posts[0] : null;
        }

        if (post is null)
        {
            logger.LogWarning(
                "Не найдено постов по тегам '{Tags}' для конфига {ConfigId}",
                config.Tags,
                config.Id
            );
            return null;
        }

        var fileUrl = post.FileUrl ?? post.SampleUrl;
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            logger.LogWarning(
                "Пост {PostId} не имеет URL файла для конфига {ConfigId}",
                post.Id,
                config.Id
            );
            return null;
        }

        using var httpClient = httpClientFactory.CreateClient();
        using var response = await httpClient.GetAsync(fileUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var fileName = Path.GetFileName(new Uri(fileUrl).AbsolutePath);

        var templateVars = new Dictionary<string, string?>
        {
            { "tags", post.Tags },
            { "id", post.Id.ToString() },
            { "rating", post.Rating },
            { "score", post.Score.ToString() },
            { "width", post.Width.ToString() },
            { "height", post.Height.ToString() },
            { "character", null },
            { "artist", null },
            { "copyright", null },
            { "source", null },
        };

        return (fileBytes, fileName, post.Id, templateVars);
    }

    private async Task<OperationResult<List<DateTime>>> ScheduleTelegramPostsAsync(
        BooruAutoPostConfig config,
        List<DateTime> scheduledTimes,
        CancellationToken cancellationToken
    )
    {
        var scheduledTimesResult = new List<DateTime>();
        const int batchSize = 200;
        var timeIndex = 0;

        while (timeIndex < scheduledTimes.Count)
        {
            try
            {
                if (config.Source == BooruSource.Danbooru)
                {
                    var (hasPosts, newTimeIndex) = await ScheduleDanbooruTelegramBatchAsync(
                        config,
                        scheduledTimes,
                        scheduledTimesResult,
                        timeIndex,
                        batchSize,
                        cancellationToken
                    );
                    timeIndex = newTimeIndex;
                    if (!hasPosts)
                    {
                        break;
                    }
                }
                else
                {
                    var (hasPosts, newTimeIndex) = await ScheduleRule34TelegramBatchAsync(
                        config,
                        scheduledTimes,
                        scheduledTimesResult,
                        timeIndex,
                        batchSize,
                        cancellationToken
                    );
                    timeIndex = newTimeIndex;
                    if (!hasPosts)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ошибка планирования поста в Telegram для конфига {ConfigId}",
                    config.Id
                );
                break;
            }
        }

        var result = OperationResult<List<DateTime>>.Ok(
            $"Запланировано {scheduledTimesResult.Count} из {scheduledTimes.Count} постов через Telegram",
            scheduledTimesResult
        );

        return result;
    }

    private async Task<(bool hasPosts, int timeIndex)> ScheduleDanbooruTelegramBatchAsync(
        BooruAutoPostConfig config,
        List<DateTime> scheduledTimes,
        List<DateTime> scheduledTimesResult,
        int timeIndex,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        DanbooruPost[]? posts;

        if (config.SpecificPostId.HasValue)
        {
            var post = await danbooruService.GetPostByIdAsync(config.SpecificPostId.Value);
            posts = post is not null ? [post] : null;
        }
        else
        {
            var fetchSize = Math.Min(scheduledTimes.Count - timeIndex, batchSize);
            posts = await danbooruService.GetRandomPostAsync(config.Tags, fetchSize);
        }

        if (posts is not { Length: > 0 })
        {
            logger.LogWarning(
                "Не найдено постов по тегам '{Tags}' для конфига {ConfigId}",
                config.Tags,
                config.Id
            );
            return (false, timeIndex);
        }

        foreach (var post in posts)
        {
            if (timeIndex >= scheduledTimes.Count)
            {
                break;
            }

            var time = scheduledTimes[timeIndex];
            timeIndex++;

            var fileUrl = post.FileUrl ?? post.LargeFileUrl;
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                continue;
            }

            var (fileBytes, fileName) = await danbooruService.DownloadFileBytesAsync(
                fileUrl,
                cancellationToken
            );

            var resolvedMessage = BooruMessageTemplateResolver.Resolve(
                config.Message,
                new Dictionary<string, string?>
                {
                    { "tags", post.TagStringGeneral },
                    { "character", post.TagStringCharacter },
                    { "artist", post.TagStringArtist },
                    { "copyright", post.TagStringCopyright },
                    { "id", post.Id.ToString() },
                    { "rating", post.Rating },
                    { "score", post.Score?.ToString() },
                    { "source", post.Source },
                }
            );

            var sendResult = await telegramPoster.SchedulePostAsync(
                config.TelegramChannelId!.Value,
                fileBytes,
                fileName,
                resolvedMessage,
                config.TelegramParseMode,
                time,
                cancellationToken
            );

            if (sendResult.Success)
            {
                scheduledTimesResult.Add(time);
            }
            else
            {
                logger.LogWarning(
                    "Не удалось запланировать пост в Telegram на {Time}: {Error}",
                    time,
                    sendResult.Message
                );
            }
        }

        return (true, timeIndex);
    }

    private async Task<(bool hasPosts, int timeIndex)> ScheduleRule34TelegramBatchAsync(
        BooruAutoPostConfig config,
        List<DateTime> scheduledTimes,
        List<DateTime> scheduledTimesResult,
        int timeIndex,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        Rule34Post[]? posts;

        var fetchSize = Math.Min(scheduledTimes.Count - timeIndex, batchSize);
        posts = await rule34Service.GetRandomPostAsync(config.Tags, fetchSize);

        if (posts is not { Length: > 0 })
        {
            logger.LogWarning(
                "Не найдено постов по тегам '{Tags}' для конфига {ConfigId}",
                config.Tags,
                config.Id
            );
            return (false, timeIndex);
        }

        foreach (var post in posts)
        {
            if (timeIndex >= scheduledTimes.Count)
            {
                break;
            }

            var time = scheduledTimes[timeIndex];
            timeIndex++;

            var fileUrl = post.FileUrl ?? post.SampleUrl;
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                continue;
            }

            using var httpClient = httpClientFactory.CreateClient();
            using var response = await httpClient.GetAsync(fileUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = Path.GetFileName(new Uri(fileUrl).AbsolutePath);

            var resolvedMessage = BooruMessageTemplateResolver.Resolve(
                config.Message,
                new Dictionary<string, string?>
                {
                    { "tags", post.Tags },
                    { "id", post.Id.ToString() },
                    { "rating", post.Rating },
                    { "score", post.Score.ToString() },
                    { "width", post.Width.ToString() },
                    { "height", post.Height.ToString() },
                }
            );

            var sendResult = await telegramPoster.SchedulePostAsync(
                config.TelegramChannelId!.Value,
                fileBytes,
                fileName,
                resolvedMessage,
                config.TelegramParseMode,
                time,
                cancellationToken
            );

            if (sendResult.Success)
            {
                scheduledTimesResult.Add(time);
            }
            else
            {
                logger.LogWarning(
                    "Не удалось запланировать пост в Telegram на {Time}: {Error}",
                    time,
                    sendResult.Message
                );
            }
        }

        return (true, timeIndex);
    }

    public async Task<OperationResult<List<BooruAutoPostConfigDto>>> GetAllAsync(
        BooruSource? source = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<BooruAutoPostConfigDto>>.Bad(
            "Не удалось получить конфигурации",
            []
        );

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var query = dbContext.BooruAutoPostConfigs.AsNoTracking();

            if (source.HasValue)
            {
                query = query.Where(c => c.Source == source.Value);
            }

            var configs = await query
                .OrderBy(c => c.Source)
                .ThenBy(c => c.TargetPlatform)
                .ThenBy(c => c.DiscordChannelId)
                .ThenBy(c => c.CreatedAtUtc)
                .Select(c => new BooruAutoPostConfigDto
                {
                    Id = c.Id,
                    Source = c.Source,
                    TargetPostCount = c.TargetPostCount,
                    SpecificPostId = c.SpecificPostId,
                    TargetPlatform = c.TargetPlatform,
                    DiscordChannelId = c.DiscordChannelId.ToString(),
                    TelegramChannelId = c.TelegramChannelId.ToString(),
                    Tags = c.Tags,
                    CronExpression = c.CronExpression,
                    PlanningHorizonDays = c.PlanningHorizonDays,
                    IsEnabled = c.IsEnabled,
                    Message = c.Message,
                    TelegramParseMode = c.TelegramParseMode,
                    LastExecutedAtUtc = c.LastExecutedAtUtc,
                    CreatedAtUtc = c.CreatedAtUtc,
                    UpdatedAtUtc = c.UpdatedAtUtc,
                })
                .ToListAsync(cancellationToken);

            await PopulateSchedulingInfoAsync(dbContext, configs, cancellationToken);

            result = OperationResult<List<BooruAutoPostConfigDto>>.Ok(
                "Конфигурации получены",
                configs
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения конфигураций BooruAutoPost");
            result = OperationResult<List<BooruAutoPostConfigDto>>.Bad(ex.Message, []);
        }

        return result;
    }

    private async Task PopulateSchedulingInfoAsync(
        AppDbContext dbContext,
        List<BooruAutoPostConfigDto> configs,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;
        var telegramChannelIds = configs
            .Where(c => c.TargetPlatform == TargetPlatform.Telegram)
            .Select(c => long.TryParse(c.TelegramChannelId, out var channelId) ? channelId : 0)
            .Where(id => id != 0)
            .Distinct()
            .ToList();

        var telegramMessagesByChannel = new Dictionary<long, List<TelegramScheduledMessageInfo>>();

        foreach (var channelId in telegramChannelIds)
        {
            var messagesResult = await telegramPoster.GetScheduledMessagesAsync(
                channelId,
                cancellationToken
            );

            if (messagesResult.Success)
            {
                telegramMessagesByChannel[channelId] = messagesResult.Data;
            }
            else
            {
                logger.LogWarning(
                    "Не удалось получить отложенные сообщения канала {ChannelId}: {Error}",
                    channelId,
                    messagesResult.Message
                );
            }
        }

        foreach (var config in configs)
        {
            if (config.TargetPlatform == TargetPlatform.Telegram)
            {
                var parsed = long.TryParse(config.TelegramChannelId, out var channelId);

                if (parsed && telegramMessagesByChannel.TryGetValue(channelId, out var messages))
                {
                    var occurrences = GetConfigOccurrences(config, now);

                    config.PendingPostsCount = TelegramScheduleMatcher.CountMatches(
                        messages,
                        occurrences
                    );
                    config.NextScheduledAtUtc = TelegramScheduleMatcher.FindEarliestMatch(
                        messages,
                        occurrences
                    );
                }
            }
            else
            {
                var pendingTimes = await dbContext
                    .BooruScheduledPosts.AsNoTracking()
                    .Where(p =>
                        p.ConfigId == config.Id
                        && p.Status == ScheduledPostStatus.Pending
                        && p.ScheduledAtUtc > now
                    )
                    .OrderBy(p => p.ScheduledAtUtc)
                    .Select(p => p.ScheduledAtUtc)
                    .ToListAsync(cancellationToken);

                config.PendingPostsCount = pendingTimes.Count;
                config.NextScheduledAtUtc = pendingTimes.Count > 0 ? pendingTimes[0] : null;
            }
        }
    }

    private static List<DateTime> GetConfigOccurrences(BooruAutoPostConfigDto config, DateTime now)
    {
        var result = new List<DateTime>();

        if (!string.IsNullOrWhiteSpace(config.CronExpression))
        {
            try
            {
                var cron = CronExpression.Parse(config.CronExpression);
                var horizonEnd = now.AddDays(config.PlanningHorizonDays);
                result = GetCronOccurrences(cron, now, horizonEnd);
            }
            catch (CronFormatException)
            {
                result = [];
            }
        }

        return result;
    }

    public async Task<OperationResult<BooruAutoPostConfigDto>> CreateAsync(
        BooruAutoPostCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<BooruAutoPostConfigDto>.Bad(
            "Не удалось создать конфигурацию",
            new BooruAutoPostConfigDto()
        );

        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return OperationResult<BooruAutoPostConfigDto>.Bad(
                validationError,
                new BooruAutoPostConfigDto()
            );
        }

        var tagValidationError = TagValidator.GetValidationError(request.Tags);
        if (tagValidationError is not null)
        {
            return OperationResult<BooruAutoPostConfigDto>.Bad(
                tagValidationError,
                new BooruAutoPostConfigDto()
            );
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var now = DateTime.UtcNow;

            ulong discordChannelId = 0;
            if (request.TargetPlatform == TargetPlatform.Discord)
            {
                ulong.TryParse(request.DiscordChannelId, out discordChannelId);
            }

            long? telegramChannelId = null;
            if (
                request.TargetPlatform == TargetPlatform.Telegram
                && long.TryParse(request.TelegramChannelId, out var parsedTelegramId)
            )
            {
                telegramChannelId = parsedTelegramId;
            }

            var entity = new BooruAutoPostConfig
            {
                Source = request.Source,
                TargetPlatform = request.TargetPlatform,
                DiscordChannelId = discordChannelId,
                TelegramChannelId = telegramChannelId,
                TargetPostCount = request.TargetPostCount,
                SpecificPostId = request.SpecificPostId,
                Tags = request.Tags.Trim(),
                CronExpression = request.CronExpression?.Trim() ?? "",
                PlanningHorizonDays = request.PlanningHorizonDays,
                Message = request.Message.Trim(),
                TelegramParseMode = request.TelegramParseMode,
                IsEnabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            dbContext.BooruAutoPostConfigs.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            result = OperationResult<BooruAutoPostConfigDto>.Ok(
                "Конфигурация создана",
                MapToDto(entity)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка создания конфигурации BooruAutoPost");
            result = OperationResult<BooruAutoPostConfigDto>.Bad(
                $"Ошибка создания: {ex.Message}",
                new BooruAutoPostConfigDto()
            );
        }

        return result;
    }

    public async Task<OperationResult<BooruAutoPostConfigDto>> UpdateAsync(
        BooruAutoPostUpdateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<BooruAutoPostConfigDto>.Bad(
            "Не удалось обновить конфигурацию",
            new BooruAutoPostConfigDto()
        );

        if (request.Id == Guid.Empty)
        {
            return OperationResult<BooruAutoPostConfigDto>.Bad(
                "Id не может быть пустым",
                new BooruAutoPostConfigDto()
            );
        }

        if (request.TargetPlatform == TargetPlatform.Discord)
        {
            var error = BooruValidationHelper.ValidateAndParseDiscordChannelId(
                request.DiscordChannelId,
                out _
            );
            if (error is not null)
            {
                return OperationResult<BooruAutoPostConfigDto>.Bad(
                    error,
                    new BooruAutoPostConfigDto()
                );
            }
        }
        else if (request.TargetPlatform == TargetPlatform.Telegram)
        {
            var error = BooruValidationHelper.ValidateAndParseTelegramChannelId(
                request.TelegramChannelId,
                out _
            );
            if (error is not null)
            {
                return OperationResult<BooruAutoPostConfigDto>.Bad(
                    error,
                    new BooruAutoPostConfigDto()
                );
            }
        }

        var cronError = BooruValidationHelper.ValidateCronExpression(request.CronExpression);
        if (cronError is not null)
        {
            return OperationResult<BooruAutoPostConfigDto>.Bad(
                cronError,
                new BooruAutoPostConfigDto()
            );
        }

        var tagValidationError = TagValidator.GetValidationError(request.Tags);
        if (tagValidationError is not null)
        {
            return OperationResult<BooruAutoPostConfigDto>.Bad(
                tagValidationError,
                new BooruAutoPostConfigDto()
            );
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var entity = await dbContext.BooruAutoPostConfigs.FindAsync(
                [request.Id],
                cancellationToken
            );

            if (entity is not null)
            {
                ulong discordChannelId = 0;
                if (request.TargetPlatform == TargetPlatform.Discord)
                {
                    ulong.TryParse(request.DiscordChannelId, out discordChannelId);
                }

                long? telegramChannelId = null;
                if (
                    request.TargetPlatform == TargetPlatform.Telegram
                    && long.TryParse(request.TelegramChannelId, out var parsedTelegramId)
                )
                {
                    telegramChannelId = parsedTelegramId;
                }

                entity.Source = request.Source;
                entity.TargetPlatform = request.TargetPlatform;
                entity.DiscordChannelId = discordChannelId;
                entity.TelegramChannelId = telegramChannelId;
                entity.TargetPostCount = request.TargetPostCount;
                entity.SpecificPostId = request.SpecificPostId;
                entity.Tags = request.Tags.Trim();
                entity.CronExpression = request.CronExpression?.Trim() ?? "";
                entity.PlanningHorizonDays = request.PlanningHorizonDays;
                entity.Message = request.Message.Trim();
                entity.TelegramParseMode = request.TelegramParseMode;
                entity.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                result = OperationResult<BooruAutoPostConfigDto>.Ok(
                    "Конфигурация обновлена",
                    MapToDto(entity)
                );
            }
            else
            {
                result = OperationResult<BooruAutoPostConfigDto>.Bad(
                    "Конфигурация не найдена",
                    new BooruAutoPostConfigDto()
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обновления конфигурации BooruAutoPost {Id}", request.Id);
            result = OperationResult<BooruAutoPostConfigDto>.Bad(
                $"Ошибка обновления: {ex.Message}",
                new BooruAutoPostConfigDto()
            );
        }

        return result;
    }

    public async Task<OperationResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult.Bad("Не удалось удалить конфигурацию");

        if (id == Guid.Empty)
        {
            return OperationResult.Bad("Id не может быть пустым");
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var entity = await dbContext.BooruAutoPostConfigs.FindAsync([id], cancellationToken);

            if (entity is not null)
            {
                dbContext.BooruAutoPostConfigs.Remove(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
                result = OperationResult.Ok("Конфигурация удалена");
            }
            else
            {
                result = OperationResult.Bad("Конфигурация не найдена");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка удаления конфигурации BooruAutoPost {Id}", id);
            result = OperationResult.Bad($"Ошибка удаления: {ex.Message}");
        }

        return result;
    }

    public async Task<OperationResult<BooruAutoPostConfigDto>> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<BooruAutoPostConfigDto>.Bad(
            "Не удалось изменить состояние",
            new BooruAutoPostConfigDto()
        );

        if (id == Guid.Empty)
        {
            return OperationResult<BooruAutoPostConfigDto>.Bad(
                "Id не может быть пустым",
                new BooruAutoPostConfigDto()
            );
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var entity = await dbContext.BooruAutoPostConfigs.FindAsync([id], cancellationToken);

            if (entity is not null)
            {
                entity.IsEnabled = isEnabled;
                entity.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                result = OperationResult<BooruAutoPostConfigDto>.Ok(
                    "Состояние обновлено",
                    MapToDto(entity)
                );
            }
            else
            {
                result = OperationResult<BooruAutoPostConfigDto>.Bad(
                    "Конфигурация не найдена",
                    new BooruAutoPostConfigDto()
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка изменения состояния BooruAutoPost {Id}", id);
            result = OperationResult<BooruAutoPostConfigDto>.Bad(
                $"Ошибка: {ex.Message}",
                new BooruAutoPostConfigDto()
            );
        }

        return result;
    }

    public async Task<OperationResult> TriggerNowAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult.Bad("Не удалось выполнить триггер");

        if (id == Guid.Empty)
        {
            return OperationResult.Bad("Id не может быть пустым");
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var config = await dbContext
                .BooruAutoPostConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (config is null)
            {
                result = OperationResult.Bad("Конфигурация не найдена");
                return result;
            }

            if (string.IsNullOrWhiteSpace(config.CronExpression))
            {
                result = OperationResult.Bad("CRON выражение не указано");
                return result;
            }

            var cron = CronExpression.Parse(config.CronExpression);
            var now = DateTime.UtcNow;
            var horizonEnd = now.AddDays(config.PlanningHorizonDays);
            var scheduledTimes = GetCronOccurrences(cron, now, horizonEnd);

            if (config.TargetPlatform == TargetPlatform.Telegram)
            {
                var channelId = config.TelegramChannelId ?? 0;

                if (channelId != 0)
                {
                    var channelMessages = await telegramPoster.GetScheduledMessagesAsync(
                        channelId,
                        cancellationToken
                    );

                    if (channelMessages.Success)
                    {
                        var staleMessages = await RemoveStaleTelegramMessagesAsync(
                            config,
                            channelId,
                            scheduledTimes,
                            channelMessages.Data,
                            cancellationToken
                        );

                        var missingTimes = TelegramScheduleMatcher.FindMissingOccurrences(
                            channelMessages.Data,
                            scheduledTimes
                        );

                        if (missingTimes.Count > 0)
                        {
                            var maxNewPosts =
                                MaxTelegramScheduledMessages
                                - (channelMessages.Data.Count - staleMessages.Count);
                            var timesToSchedule =
                                maxNewPosts > 0
                                    ? missingTimes.Take(maxNewPosts).ToList()
                                    : [];

                            if (timesToSchedule.Count > 0)
                            {
                                var scheduleResult = await ScheduleTelegramPostsAsync(
                                    config,
                                    timesToSchedule,
                                    cancellationToken
                                );

                                result = OperationResult.Ok(scheduleResult.Message);
                            }
                            else
                            {
                                result = OperationResult.Ok(
                                    "Достигнут лимит отложенных сообщений Telegram для этого канала"
                                );
                            }
                        }
                        else
                        {
                            result = OperationResult.Ok(
                                "Все планируемые времена уже покрыты отложенными сообщениями в Telegram"
                            );
                        }
                    }
                    else
                    {
                        result = OperationResult.Bad(channelMessages.Message);
                    }
                }
                else
                {
                    result = OperationResult.Bad("Telegram канал не указан");
                }
            }
            else
            {
                result = await ScheduleDiscordPostsAsync(
                    dbContext,
                    config,
                    scheduledTimes,
                    now,
                    horizonEnd,
                    cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка ручного триггера BooruAutoPost {Id}", id);
            result = OperationResult.Bad($"Ошибка: {ex.Message}");
        }

        return result;
    }

    private async Task<OperationResult> ScheduleDiscordPostsAsync(
        AppDbContext dbContext,
        BooruAutoPostConfig config,
        List<DateTime> scheduledTimes,
        DateTime now,
        DateTime horizonEnd,
        CancellationToken cancellationToken
    )
    {
        var pendingCount = await dbContext
            .BooruScheduledPosts.AsNoTracking()
            .CountAsync(
                p => p.ConfigId == config.Id && p.Status == ScheduledPostStatus.Pending,
                cancellationToken
            );

        if (pendingCount > 0)
        {
            return OperationResult.Ok(
                $"Уже запланировано {pendingCount} постов. Дождитесь выполнения или отмените текущие."
            );
        }

        var newPosts = scheduledTimes
            .Select(time => new BooruScheduledPost
            {
                ConfigId = config.Id,
                Source = config.Source,
                ScheduledAtUtc = time,
                Status = ScheduledPostStatus.Pending,
                CreatedAtUtc = now,
            })
            .ToList();

        dbContext.BooruScheduledPosts.AddRange(newPosts);
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Ok(
            $"Запланировано {newPosts.Count} постов до {horizonEnd:dd.MM.yyyy HH:mm}"
        );
    }

    public async Task<OperationResult<List<DiscordChannelOptionDto>>> GetDiscordChannelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<DiscordChannelOptionDto>>.Bad(
            "Не удалось получить Discord каналы",
            []
        );

        try
        {
            var client = discordGatewayService.Client;
            if (client is null)
            {
                client = await discordGatewayService.EnsureConnectedAsync(cancellationToken);
            }

            if (client is not null)
            {
                var channels = client
                    .Guilds.Values.SelectMany(guild =>
                        guild
                            .Channels.Values.Where(channel =>
                            {
                                var channelType = channel.Type.ToString();
                                return channelType is "Text" or "Announcement";
                            })
                            .Select(channel => new DiscordChannelOptionDto
                            {
                                Id = channel.Id.ToString(),
                                Name = channel.Name,
                                GuildId = guild.Id.ToString(),
                                GuildName = guild.Name,
                            })
                    )
                    .OrderBy(e => e.GuildName)
                    .ThenBy(e => e.Name)
                    .ThenBy(e => e.Id)
                    .ToList();

                result = OperationResult<List<DiscordChannelOptionDto>>.Ok(
                    "Discord каналы получены",
                    channels
                );
            }
            else
            {
                result = OperationResult<List<DiscordChannelOptionDto>>.Bad(
                    "Discord клиент недоступен",
                    []
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения Discord каналов для BooruAutoPost");
            result = OperationResult<List<DiscordChannelOptionDto>>.Bad(
                $"Ошибка: {ex.Message}",
                []
            );
        }

        return result;
    }

    public async Task<OperationResult<List<TelegramChannelOptionDto>>> GetTelegramChannelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<TelegramChannelOptionDto>>.Bad(
            "Не удалось получить Telegram каналы",
            []
        );

        try
        {
            var channelsResult = await telegramPoster.GetScheduledMessagesAsync(0, cancellationToken);
            result = OperationResult<List<TelegramChannelOptionDto>>.Ok(
                "Telegram каналы получены",
                []
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения Telegram каналов для BooruAutoPost");
            result = OperationResult<List<TelegramChannelOptionDto>>.Bad(
                $"Ошибка: {ex.Message}",
                []
            );
        }

        return result;
    }

    private static string? ValidateCreateRequest(BooruAutoPostCreateRequest request)
    {
        string? result;

        if (request.TargetPlatform == TargetPlatform.Discord)
        {
            result = BooruValidationHelper.ValidateAndParseDiscordChannelId(
                request.DiscordChannelId,
                out _
            );
        }
        else if (request.TargetPlatform == TargetPlatform.Telegram)
        {
            result = BooruValidationHelper.ValidateAndParseTelegramChannelId(
                request.TelegramChannelId,
                out _
            );
        }
        else
        {
            result = null;
        }

        if (result is not null)
        {
            return result;
        }

        result = BooruValidationHelper.ValidateCronExpression(request.CronExpression);

        return result;
    }

    private static BooruAutoPostConfigDto MapToDto(BooruAutoPostConfig entity)
    {
        return new BooruAutoPostConfigDto
        {
            Id = entity.Id,
            Source = entity.Source,
            TargetPostCount = entity.TargetPostCount,
            SpecificPostId = entity.SpecificPostId,
            TargetPlatform = entity.TargetPlatform,
            DiscordChannelId = entity.DiscordChannelId.ToString(),
            TelegramChannelId = entity.TelegramChannelId?.ToString(),
            Tags = entity.Tags,
            CronExpression = entity.CronExpression,
            PlanningHorizonDays = entity.PlanningHorizonDays,
            IsEnabled = entity.IsEnabled,
            Message = entity.Message,
            TelegramParseMode = entity.TelegramParseMode,
            LastExecutedAtUtc = entity.LastExecutedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }

    private async Task NotifyAdminsAboutErrorAsync(
        Guid postId,
        BooruAutoPostConfig config,
        Exception error,
        CancellationToken cancellationToken
    )
    {
        var adminIds = telegramConfig.Value.AdminIdsArray ?? [];
        if (adminIds.Length == 0)
        {
            return;
        }

        var message = $"""
            <b>BooruAutoPost: ошибка отправки</b>

            Пост: {postId}
            Источник: {config.Source}
            Платформа: {config.TargetPlatform}
            Конфиг: {config.Id}
            Ошибка: {error.Message}
            """;

        foreach (var adminId in adminIds)
        {
            try
            {
                await telegramBotClient.SendMessage(
                    adminId,
                    message,
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception notifyEx)
            {
                logger.LogError(
                    notifyEx,
                    "Не удалось отправить уведомление об ошибке админу {AdminId}",
                    adminId
                );
            }
        }
    }
}
