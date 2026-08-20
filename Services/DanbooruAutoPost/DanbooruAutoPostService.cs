using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.BooruShared;
using MARS.Server.Services.BooruShared.Entities;
using MARS.Server.Services.DanbooruAutoPost.Entities;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using MARS.Server.Services.Twitch.Rewards._27_RandomArt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.DanbooruAutoPost;

public class DanbooruAutoPostService(
    ILogger<DanbooruAutoPostService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    DanbooruRandomPostService danbooruService,
    IDanbooruDiscordPoster discordPoster,
    IDanbooruTelegramPoster telegramPoster,
    IDiscordGatewayService discordGatewayService,
    IDeduplicationService deduplicationService,
    ITelegramBotClient telegramBotClient,
    IOptions<TelegramConfiguration> telegramConfig
) : BackgroundService, IDanbooruAutoPostService
{
    private const string Source = "Danbooru";
    private const int MaxDedupRetries = 5;
    private const int SslRetryBaseDelaySeconds = 5;
    private const int SslRetryMaxDelaySeconds = 300;

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
                logger.LogError(ex, "Ошибка в цикле DanbooruAutoPostService");
            }
        }
    }

    private async Task ProcessScheduledPostsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var duePosts = await dbContext
            .DanbooruScheduledPosts.AsNoTracking()
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

                var entity = await dbContext.DanbooruScheduledPosts.FindAsync(
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
                var configEntity = await updateContext.DanbooruAutoPostConfigs.FindAsync(
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

                            var successEntity = await dbContext.DanbooruScheduledPosts.FindAsync(
                                [scheduledPost.Id],
                                cancellationToken
                            );
                            if (successEntity is not null)
                            {
                                successEntity.Status = ScheduledPostStatus.Posted;
                                successEntity.PostedAtUtc = DateTime.UtcNow;
                                await dbContext.SaveChangesAsync(cancellationToken);
                            }

                            await using var updateCtx = await dbContextFactory.CreateDbContextAsync(
                                cancellationToken
                            );
                            var cfgEntity = await updateCtx.DanbooruAutoPostConfigs.FindAsync(
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

                                await using var nonSslErrorCtx =
                                    await dbContextFactory.CreateDbContextAsync(cancellationToken);
                                var nonSslErrorEntity =
                                    await nonSslErrorCtx.DanbooruScheduledPosts.FindAsync(
                                        [scheduledPost.Id],
                                        cancellationToken
                                    );
                                if (nonSslErrorEntity is not null)
                                {
                                    nonSslErrorEntity.Status = ScheduledPostStatus.Failed;
                                    nonSslErrorEntity.ErrorMessage = retryEx.Message;
                                    await nonSslErrorCtx.SaveChangesAsync(cancellationToken);
                                }

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

                    await using var errorContext = await dbContextFactory.CreateDbContextAsync(
                        cancellationToken
                    );
                    var errorEntity = await errorContext.DanbooruScheduledPosts.FindAsync(
                        [scheduledPost.Id],
                        cancellationToken
                    );
                    if (errorEntity is not null)
                    {
                        errorEntity.Status = ScheduledPostStatus.Failed;
                        errorEntity.ErrorMessage = ex.Message;
                        await errorContext.SaveChangesAsync(cancellationToken);
                    }

                    await NotifyAdminsAboutErrorAsync(
                        scheduledPost.Id,
                        config,
                        ex,
                        cancellationToken
                    );
                }
            }
        }

        var enabledConfigs = await dbContext
            .DanbooruAutoPostConfigs.AsNoTracking()
            .Where(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.CronExpression))
            .ToListAsync(cancellationToken);

        foreach (var config in enabledConfigs)
        {
            try
            {
                var hasUpcomingPosts = false;
                if (config.TargetPlatform == TargetPlatform.Telegram)
                {
                    hasUpcomingPosts = await dbContext
                        .DanbooruScheduledPosts.AsNoTracking()
                        .AnyAsync(
                            p =>
                                p.ConfigId == config.Id
                                && p.Status == ScheduledPostStatus.Posted
                                && p.ScheduledAtUtc > now,
                            cancellationToken
                        );

                    if (!hasUpcomingPosts)
                    {
                        await CancelStaleTelegramPostsAsync(
                            dbContext,
                            config.Id,
                            cancellationToken
                        );
                    }
                }
                else
                {
                    hasUpcomingPosts = await dbContext
                        .DanbooruScheduledPosts.AsNoTracking()
                        .AnyAsync(
                            p => p.ConfigId == config.Id && p.Status == ScheduledPostStatus.Pending,
                            cancellationToken
                        );
                }

                if (hasUpcomingPosts)
                {
                    continue;
                }

                var cron = CronExpression.Parse(config.CronExpression);
                var horizonEnd = now.AddDays(config.PlanningHorizonDays);

                var scheduledTimes = new List<DateTime>();
                var nextOccurrence = cron.GetNextOccurrence(now.AddMinutes(-1));

                while (nextOccurrence.HasValue && nextOccurrence.Value <= horizonEnd)
                {
                    scheduledTimes.Add(nextOccurrence.Value);
                    nextOccurrence = cron.GetNextOccurrence(nextOccurrence.Value);
                }

                if (config.TargetPlatform == TargetPlatform.Telegram)
                {
                    var scheduleResult = await ScheduleTelegramPostsAsync(
                        config,
                        scheduledTimes,
                        cancellationToken
                    );

                    if (scheduleResult.Success && scheduleResult.Data.Count > 0)
                    {
                        var telegramPosts = scheduleResult
                            .Data.Select(time => new DanbooruScheduledPost
                            {
                                ConfigId = config.Id,
                                ScheduledAtUtc = time,
                                Status = ScheduledPostStatus.Posted,
                                CreatedAtUtc = now,
                            })
                            .ToList();

                        dbContext.DanbooruScheduledPosts.AddRange(telegramPosts);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                else
                {
                    var discordPosts = scheduledTimes
                        .Select(time => new DanbooruScheduledPost
                        {
                            ConfigId = config.Id,
                            ScheduledAtUtc = time,
                            Status = ScheduledPostStatus.Pending,
                            CreatedAtUtc = now,
                        })
                        .ToList();

                    dbContext.DanbooruScheduledPosts.AddRange(discordPosts);
                    await dbContext.SaveChangesAsync(cancellationToken);
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

    private async Task PostImageAsync(
        DanbooruAutoPostConfig config,
        CancellationToken cancellationToken
    )
    {
        var channelId =
            config.TargetPlatform == TargetPlatform.Telegram
                ? (ulong)Math.Abs(config.TelegramChannelId ?? 0)
                : config.DiscordChannelId;

        for (var attempt = 0; attempt <= MaxDedupRetries; attempt++)
        {
            DanbooruPost? post;

            if (config.DanbooruPostId.HasValue)
            {
                post = await danbooruService.GetPostByIdAsync(config.DanbooruPostId.Value);
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
                return;
            }

            if (
                await deduplicationService.IsAlreadyPostedAsync(
                    Source,
                    post.Id,
                    channelId,
                    cancellationToken
                )
            )
            {
                if (config.DanbooruPostId.HasValue)
                {
                    logger.LogInformation(
                        "Изображение {PostId} уже отправлено в канал {ChannelId}",
                        post.Id,
                        channelId
                    );
                    return;
                }

                logger.LogInformation(
                    "Изображение {PostId} уже отправлено в канал {ChannelId}, попытка {Attempt}/{Max}",
                    post.Id,
                    channelId,
                    attempt + 1,
                    MaxDedupRetries
                );
                continue;
            }

            var fileUrl = post.FileUrl ?? post.LargeFileUrl;
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                logger.LogWarning(
                    "Пост {PostId} не имеет URL файла для конфига {ConfigId}",
                    post.Id,
                    config.Id
                );
                return;
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

            OperationResult sendResult;

            if (config.TargetPlatform == TargetPlatform.Telegram)
            {
                sendResult = await telegramPoster.PostAsync(
                    config.TelegramChannelId!.Value,
                    fileBytes,
                    fileName,
                    resolvedMessage,
                    config.TelegramParseMode,
                    cancellationToken
                );
            }
            else
            {
                sendResult = await discordPoster.PostAsync(
                    config.DiscordChannelId,
                    fileBytes,
                    fileName,
                    string.IsNullOrWhiteSpace(resolvedMessage) ? null : resolvedMessage,
                    cancellationToken
                );
            }

            if (sendResult.Success)
            {
                await deduplicationService.RecordPostAsync(
                    Source,
                    post.Id,
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

    public async Task<OperationResult<List<DanbooruAutoPostConfigDto>>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
            "Не удалось получить конфигурации",
            []
        );

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var configs = await dbContext
                .DanbooruAutoPostConfigs.AsNoTracking()
                .OrderBy(c => c.TargetPlatform)
                .ThenBy(c => c.DiscordChannelId)
                .ThenBy(c => c.CreatedAtUtc)
                .Select(c => new DanbooruAutoPostConfigDto
                {
                    Id = c.Id,
                    TargetPostCount = c.TargetPostCount,
                    DanbooruPostId = c.DanbooruPostId,
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

            result = OperationResult<List<DanbooruAutoPostConfigDto>>.Ok(
                "Конфигурации получены",
                configs
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения конфигураций DanbooruAutoPost");
            result = OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(ex.Message, []);
        }

        return result;
    }

    public async Task<OperationResult<DanbooruAutoPostConfigDto>> CreateAsync(
        DanbooruAutoPostCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<DanbooruAutoPostConfigDto>.Bad(
            "Не удалось создать конфигурацию",
            new DanbooruAutoPostConfigDto()
        );

        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                validationError,
                new DanbooruAutoPostConfigDto()
            );
        }

        var tagValidationError = TagValidator.GetValidationError(request.Tags);
        if (tagValidationError is not null)
        {
            return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                tagValidationError,
                new DanbooruAutoPostConfigDto()
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

            var entity = new DanbooruAutoPostConfig
            {
                TargetPlatform = request.TargetPlatform,
                DiscordChannelId = discordChannelId,
                TelegramChannelId = telegramChannelId,
                Tags = request.Tags.Trim(),
                CronExpression = request.CronExpression?.Trim() ?? "",
                PlanningHorizonDays = request.PlanningHorizonDays,
                Message = request.Message.Trim(),
                TelegramParseMode = request.TelegramParseMode,
                IsEnabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            dbContext.DanbooruAutoPostConfigs.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            result = OperationResult<DanbooruAutoPostConfigDto>.Ok(
                "Конфигурация создана",
                MapToDto(entity)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка создания конфигурации DanbooruAutoPost");
            result = OperationResult<DanbooruAutoPostConfigDto>.Bad(
                $"Ошибка создания: {ex.Message}",
                new DanbooruAutoPostConfigDto()
            );
        }

        return result;
    }

    public async Task<OperationResult<DanbooruAutoPostConfigDto>> UpdateAsync(
        DanbooruAutoPostUpdateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<DanbooruAutoPostConfigDto>.Bad(
            "Не удалось обновить конфигурацию",
            new DanbooruAutoPostConfigDto()
        );

        if (request.Id == Guid.Empty)
        {
            return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                "Id не может быть пустым",
                new DanbooruAutoPostConfigDto()
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
                return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                    error,
                    new DanbooruAutoPostConfigDto()
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
                return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                    error,
                    new DanbooruAutoPostConfigDto()
                );
            }
        }

        var cronError = BooruValidationHelper.ValidateCronExpression(request.CronExpression);
        if (cronError is not null)
        {
            return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                cronError,
                new DanbooruAutoPostConfigDto()
            );
        }

        var tagValidationError = TagValidator.GetValidationError(request.Tags);
        if (tagValidationError is not null)
        {
            return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                tagValidationError,
                new DanbooruAutoPostConfigDto()
            );
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var entity = await dbContext.DanbooruAutoPostConfigs.FindAsync(
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

                entity.TargetPlatform = request.TargetPlatform;
                entity.DiscordChannelId = discordChannelId;
                entity.TelegramChannelId = telegramChannelId;
                entity.Tags = request.Tags.Trim();
                entity.CronExpression = request.CronExpression?.Trim() ?? "";
                entity.PlanningHorizonDays = request.PlanningHorizonDays;
                entity.Message = request.Message.Trim();
                entity.TelegramParseMode = request.TelegramParseMode;
                entity.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                result = OperationResult<DanbooruAutoPostConfigDto>.Ok(
                    "Конфигурация обновлена",
                    MapToDto(entity)
                );
            }
            else
            {
                result = OperationResult<DanbooruAutoPostConfigDto>.Bad(
                    "Конфигурация не найдена",
                    new DanbooruAutoPostConfigDto()
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обновления конфигурации DanbooruAutoPost {Id}", request.Id);
            result = OperationResult<DanbooruAutoPostConfigDto>.Bad(
                $"Ошибка обновления: {ex.Message}",
                new DanbooruAutoPostConfigDto()
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
            var entity = await dbContext.DanbooruAutoPostConfigs.FindAsync([id], cancellationToken);

            if (entity is not null)
            {
                dbContext.DanbooruAutoPostConfigs.Remove(entity);
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
            logger.LogError(ex, "Ошибка удаления конфигурации DanbooruAutoPost {Id}", id);
            result = OperationResult.Bad($"Ошибка удаления: {ex.Message}");
        }

        return result;
    }

    public async Task<OperationResult<DanbooruAutoPostConfigDto>> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<DanbooruAutoPostConfigDto>.Bad(
            "Не удалось изменить состояние",
            new DanbooruAutoPostConfigDto()
        );

        if (id == Guid.Empty)
        {
            return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                "Id не может быть пустым",
                new DanbooruAutoPostConfigDto()
            );
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var entity = await dbContext.DanbooruAutoPostConfigs.FindAsync([id], cancellationToken);

            if (entity is not null)
            {
                entity.IsEnabled = isEnabled;
                entity.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                result = OperationResult<DanbooruAutoPostConfigDto>.Ok(
                    "Состояние обновлено",
                    MapToDto(entity)
                );
            }
            else
            {
                result = OperationResult<DanbooruAutoPostConfigDto>.Bad(
                    "Конфигурация не найдена",
                    new DanbooruAutoPostConfigDto()
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка изменения состояния DanbooruAutoPost {Id}", id);
            result = OperationResult<DanbooruAutoPostConfigDto>.Bad(
                $"Ошибка: {ex.Message}",
                new DanbooruAutoPostConfigDto()
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
                .DanbooruAutoPostConfigs.AsNoTracking()
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

            var scheduledTimes = new List<DateTime>();
            var nextOccurrence = cron.GetNextOccurrence(now.AddMinutes(-1));

            while (nextOccurrence.HasValue && nextOccurrence.Value <= horizonEnd)
            {
                scheduledTimes.Add(nextOccurrence.Value);
                nextOccurrence = cron.GetNextOccurrence(nextOccurrence.Value);
            }

            if (config.TargetPlatform == TargetPlatform.Telegram)
            {
                var hasUpcomingPosts = await dbContext
                    .DanbooruScheduledPosts.AsNoTracking()
                    .AnyAsync(
                        p =>
                            p.ConfigId == config.Id
                            && p.Status == ScheduledPostStatus.Posted
                            && p.ScheduledAtUtc > now,
                        cancellationToken
                    );

                if (!hasUpcomingPosts)
                {
                    await CancelStaleTelegramPostsAsync(dbContext, config.Id, cancellationToken);
                }

                if (hasUpcomingPosts)
                {
                    result = OperationResult.Ok(
                        "Уже запланированы отложенные посты в Telegram. Дождитесь доставки или отмените текущие."
                    );
                }
                else
                {
                    var scheduleResult = await ScheduleTelegramPostsAsync(
                        config,
                        scheduledTimes,
                        cancellationToken
                    );

                    if (scheduleResult.Success && scheduleResult.Data.Count > 0)
                    {
                        var newPosts = scheduleResult
                            .Data.Select(time => new DanbooruScheduledPost
                            {
                                ConfigId = config.Id,
                                ScheduledAtUtc = time,
                                Status = ScheduledPostStatus.Posted,
                                CreatedAtUtc = now,
                            })
                            .ToList();

                        dbContext.DanbooruScheduledPosts.AddRange(newPosts);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }

                    result = OperationResult.Ok(scheduleResult.Message);
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
            logger.LogError(ex, "Ошибка ручного триггера DanbooruAutoPost {Id}", id);
            result = OperationResult.Bad($"Ошибка: {ex.Message}");
        }

        return result;
    }

    private async Task<OperationResult<List<DateTime>>> ScheduleTelegramPostsAsync(
        DanbooruAutoPostConfig config,
        List<DateTime> scheduledTimes,
        CancellationToken cancellationToken
    )
    {
        var scheduledTimesResult = new List<DateTime>();
        const int danbooruBatchSize = 200;
        var timeIndex = 0;

        while (timeIndex < scheduledTimes.Count)
        {
            try
            {
                DanbooruPost[]? posts;

                if (config.DanbooruPostId.HasValue)
                {
                    var post = await danbooruService.GetPostByIdAsync(config.DanbooruPostId.Value);
                    posts = post is not null ? [post] : null;
                }
                else
                {
                    var batchSize = Math.Min(scheduledTimes.Count - timeIndex, danbooruBatchSize);
                    posts = await danbooruService.GetRandomPostAsync(config.Tags, batchSize);
                }

                if (posts is not { Length: > 0 })
                {
                    logger.LogWarning(
                        "Не найдено постов по тегам '{Tags}' для конфига {ConfigId}",
                        config.Tags,
                        config.Id
                    );
                    break;
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

    private static async Task CancelStaleTelegramPostsAsync(
        AppDbContext dbContext,
        Guid configId,
        CancellationToken cancellationToken
    )
    {
        var stalePosts = await dbContext
            .DanbooruScheduledPosts.Where(p =>
                p.ConfigId == configId
                && (
                    p.Status == ScheduledPostStatus.Pending
                    || p.Status == ScheduledPostStatus.Failed
                )
            )
            .ToListAsync(cancellationToken);

        foreach (var stalePost in stalePosts)
        {
            stalePost.Status = ScheduledPostStatus.Cancelled;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<OperationResult> ScheduleDiscordPostsAsync(
        AppDbContext dbContext,
        DanbooruAutoPostConfig config,
        List<DateTime> scheduledTimes,
        DateTime now,
        DateTime horizonEnd,
        CancellationToken cancellationToken
    )
    {
        var pendingCount = await dbContext
            .DanbooruScheduledPosts.AsNoTracking()
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
            .Select(time => new DanbooruScheduledPost
            {
                ConfigId = config.Id,
                ScheduledAtUtc = time,
                Status = ScheduledPostStatus.Pending,
                CreatedAtUtc = now,
            })
            .ToList();

        dbContext.DanbooruScheduledPosts.AddRange(newPosts);
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
            logger.LogError(ex, "Ошибка получения Discord каналов для DanbooruAutoPost");
            result = OperationResult<List<DiscordChannelOptionDto>>.Bad(
                $"Ошибка: {ex.Message}",
                []
            );
        }

        return result;
    }

    private static string? ValidateCreateRequest(DanbooruAutoPostCreateRequest request)
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

    private static DanbooruAutoPostConfigDto MapToDto(DanbooruAutoPostConfig entity)
    {
        return new DanbooruAutoPostConfigDto
        {
            Id = entity.Id,
            TargetPostCount = entity.TargetPostCount,
            DanbooruPostId = entity.DanbooruPostId,
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
        DanbooruAutoPostConfig config,
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
            <b>DanbooruAutoPost: ошибка отправки</b>

            Пост: {postId}
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
