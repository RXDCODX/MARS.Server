using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.BooruShared;
using MARS.Server.Services.DanbooruAutoPost.Entities;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.Telegram.DiscordBridge;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using MARS.Server.Services.Twitch.Rewards._27_RandomArt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MARS.Server.Services.DanbooruAutoPost;

public class DanbooruAutoPostService(
    ILogger<DanbooruAutoPostService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    DanbooruRandomPostService danbooruService,
    IDiscordGatewayService discordGatewayService,
    IDeduplicationService deduplicationService,
    ITelegramBotClient telegramBotClient,
    ITelegramDiscordBridgeService telegramDiscordBridgeService
) : BackgroundService, IDanbooruAutoPostService
{
    private const string Source = "Danbooru";
    private const int MaxDedupRetries = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessScheduledPostsAsync(stoppingToken);
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

        var configs = await dbContext
            .DanbooruAutoPostConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var config in configs)
        {
            try
            {
                var shouldPost = false;

                if (config.ScheduledAtUtc.HasValue && config.ScheduledAtUtc.Value <= now)
                {
                    shouldPost = true;
                }
                else if (
                    !string.IsNullOrWhiteSpace(config.CronExpression)
                    && !config.ScheduledAtUtc.HasValue
                )
                {
                    var cron = CronExpression.Parse(config.CronExpression);
                    var lastExecuted = config.LastExecutedAtUtc.HasValue
                        ? DateTime.SpecifyKind(config.LastExecutedAtUtc.Value, DateTimeKind.Utc)
                        : (DateTime?)null;

                    var nextOccurrence = lastExecuted.HasValue
                        ? cron.GetNextOccurrence(lastExecuted.Value)
                        : cron.GetNextOccurrence(now.AddMinutes(-1));

                    if (nextOccurrence.HasValue && nextOccurrence.Value <= now)
                    {
                        shouldPost = true;
                    }
                }

                if (shouldPost)
                {
                    await PostImageAsync(config, cancellationToken);

                    await using var updateContext = await dbContextFactory.CreateDbContextAsync(
                        cancellationToken
                    );
                    var entity = await updateContext.DanbooruAutoPostConfigs.FindAsync(
                        [config.Id],
                        cancellationToken
                    );
                    if (entity is not null)
                    {
                        entity.LastExecutedAtUtc = now;

                        if (config.ScheduledAtUtc.HasValue)
                        {
                            entity.ScheduledAtUtc = null;
                            entity.IsEnabled = false;
                        }

                        await updateContext.SaveChangesAsync(cancellationToken);
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
                    "Ошибка отправки изображения для конфига {ConfigId}",
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
        var channelKey = GetChannelKey(config);

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
                    channelKey,
                    cancellationToken
                )
            )
            {
                if (config.DanbooruPostId.HasValue)
                {
                    logger.LogInformation(
                        "Изображение {PostId} уже отправлено в канал {ChannelKey}",
                        post.Id,
                        channelKey
                    );
                    return;
                }

                logger.LogInformation(
                    "Изображение {PostId} уже отправлено в канал {ChannelKey}, попытка {Attempt}/{Max}",
                    post.Id,
                    channelKey,
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
            var tagPreview = string.IsNullOrWhiteSpace(post.TagStringCharacter)
                ? post
                    .TagStringGeneral?.Split(' ')
                    .Take(5)
                    .Aggregate("", (a, b) => $"{a} {b}")
                    .Trim()
                : post.TagStringCharacter;

            var caption =
                $"Danbooru | Score: {post.Score} | Rating: {post.Rating}\n"
                + $"Tags: {tagPreview}\n"
                + $"https://danbooru.donmai.us/posts/{post.Id}";

            OperationResult sendResult;

            if (config.TargetPlatform == TargetPlatform.Telegram)
            {
                sendResult = await PostToTelegramAsync(
                    config,
                    fileBytes,
                    fileName,
                    caption,
                    cancellationToken
                );
            }
            else
            {
                await using var stream = new MemoryStream(fileBytes);
                var message =
                    $"**Danbooru** | Score: {post.Score} | Rating: {post.Rating}\n"
                    + $"Tags: {tagPreview}\n"
                    + $"https://danbooru.donmai.us/posts/{post.Id}";

                var discordResult = await discordGatewayService.SendFileAsync(
                    config.DiscordChannelId,
                    stream,
                    fileName,
                    message,
                    cancellationToken
                );
                sendResult = discordResult;
            }

            if (sendResult.Success)
            {
                await deduplicationService.RecordPostAsync(
                    Source,
                    post.Id,
                    channelKey,
                    cancellationToken
                );
            }
            else
            {
                logger.LogWarning(
                    "Не удалось отправить изображение в {Platform} канал {ChannelKey}: {Error}",
                    config.TargetPlatform,
                    channelKey,
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

    private async Task<OperationResult> PostToTelegramAsync(
        DanbooruAutoPostConfig config,
        byte[] fileBytes,
        string fileName,
        string caption,
        CancellationToken cancellationToken
    )
    {
        var result = OperationResult.Bad("Не удалось отправить в Telegram");

        try
        {
            if (config.TelegramChannelId is null or 0)
            {
                return OperationResult.Bad("TelegramChannelId не указан");
            }

            await using var stream = new MemoryStream(fileBytes);
            var inputFile = InputFile.FromStream(stream, fileName);

            await telegramBotClient.SendPhoto(
                chatId: config.TelegramChannelId.Value,
                photo: inputFile,
                caption: caption,
                cancellationToken: cancellationToken
            );

            result = OperationResult.Ok("Изображение отправлено в Telegram");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка отправки в Telegram канал {ChannelId}",
                config.TelegramChannelId
            );
            result = OperationResult.Bad($"Ошибка Telegram: {ex.Message}");
        }

        return result;
    }

    private static string GetChannelKey(DanbooruAutoPostConfig config)
    {
        return config.TargetPlatform == TargetPlatform.Telegram
            ? $"tg_{config.TelegramChannelId}"
            : $"dc_{config.DiscordChannelId}";
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
                    BatchId = c.BatchId,
                    DanbooruPostId = c.DanbooruPostId,
                    TargetPlatform = c.TargetPlatform,
                    DiscordChannelId = c.DiscordChannelId,
                    TelegramChannelId = c.TelegramChannelId,
                    Tags = c.Tags,
                    CronExpression = c.CronExpression,
                    ScheduledAtUtc = c.ScheduledAtUtc,
                    IsEnabled = c.IsEnabled,
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
            var entity = new DanbooruAutoPostConfig
            {
                TargetPlatform = request.TargetPlatform,
                DiscordChannelId =
                    request.TargetPlatform == TargetPlatform.Discord ? request.DiscordChannelId : 0,
                TelegramChannelId =
                    request.TargetPlatform == TargetPlatform.Telegram
                        ? request.TelegramChannelId
                        : null,
                Tags = request.Tags.Trim(),
                CronExpression = request.CronExpression?.Trim() ?? "",
                ScheduledAtUtc = request.ScheduledAtUtc,
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

    public async Task<OperationResult<List<DanbooruAutoPostConfigDto>>> BatchCreateAsync(
        DanbooruAutoPostBatchCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
            "Не удалось создать пакет конфигураций",
            []
        );

        if (
            request
            is { TargetPlatform: TargetPlatform.Discord, DiscordChannelId: 0 }
                or { TargetPlatform: TargetPlatform.Telegram, TelegramChannelId: null or 0 }
        )
        {
            return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad("Канал не указан", []);
        }

        if (string.IsNullOrWhiteSpace(request.CronExpression))
        {
            return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                "CRON выражение обязательно",
                []
            );
        }

        CronExpression cron;
        try
        {
            cron = CronExpression.Parse(request.CronExpression);
        }
        catch (CronFormatException)
        {
            return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                "Некорректное CRON выражение",
                []
            );
        }

        if (request.EndAtUtc <= DateTime.UtcNow)
        {
            return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                "Дата окончания должна быть в будущем",
                []
            );
        }

        var tagValidationError = TagValidator.GetValidationError(request.Tags);
        if (tagValidationError is not null)
        {
            return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(tagValidationError, []);
        }

        try
        {
            var now = DateTime.UtcNow;
            var batchId = Guid.NewGuid();
            var timeSlots = GenerateTimeSlots(cron, now, request.EndAtUtc);

            if (timeSlots.Count == 0)
            {
                return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                    "Не удалось вычислить слоты расписания",
                    []
                );
            }

            var channelKey = GetChannelKey(
                request.TargetPlatform,
                request.DiscordChannelId,
                request.TelegramChannelId
            );

            var images = await FetchUniqueImagesAsync(
                request.Tags,
                timeSlots.Count,
                channelKey,
                cancellationToken
            );

            if (images.Count == 0)
            {
                return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                    "Не найдено доступных изображений",
                    []
                );
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var slotCount = Math.Min(timeSlots.Count, images.Count);
            var entities = new List<DanbooruAutoPostConfig>();

            for (var i = 0; i < slotCount; i++)
            {
                var image = images[i];
                var imageTags = string.IsNullOrWhiteSpace(image.TagStringCharacter)
                    ? image
                        .TagStringGeneral?.Split(' ')
                        .Take(2)
                        .Aggregate("", (a, b) => $"{a} {b}")
                        .Trim()
                    : image.TagStringCharacter;

                var entity = new DanbooruAutoPostConfig
                {
                    BatchId = batchId,
                    DanbooruPostId = image.Id,
                    TargetPlatform = request.TargetPlatform,
                    DiscordChannelId =
                        request.TargetPlatform == TargetPlatform.Discord
                            ? request.DiscordChannelId
                            : 0,
                    TelegramChannelId =
                        request.TargetPlatform == TargetPlatform.Telegram
                            ? request.TelegramChannelId
                            : null,
                    Tags = (imageTags ?? request.Tags).Trim(),
                    CronExpression = "",
                    ScheduledAtUtc = timeSlots[i],
                    IsEnabled = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                };
                entities.Add(entity);
            }

            dbContext.DanbooruAutoPostConfigs.AddRange(entities);
            await dbContext.SaveChangesAsync(cancellationToken);

            var dtos = entities.Select(MapToDto).ToList();
            result = OperationResult<List<DanbooruAutoPostConfigDto>>.Ok(
                $"Создано {entities.Count} отложенных постов (батч {batchId})",
                dtos
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка пакетного создания DanbooruAutoPost");
            result = OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                $"Ошибка: {ex.Message}",
                []
            );
        }

        return result;
    }

    public async Task<OperationResult<List<DanbooruAutoPostConfigDto>>> RescheduleBatchAsync(
        Guid batchId,
        DanbooruAutoPostRescheduleRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
            "Не удалось перепланировать батч",
            []
        );

        if (batchId == Guid.Empty)
        {
            return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                "BatchId не может быть пустым",
                []
            );
        }

        CronExpression cron;
        try
        {
            cron = CronExpression.Parse(request.NewCronExpression);
        }
        catch (CronFormatException)
        {
            return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                "Некорректное CRON выражение",
                []
            );
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var now = DateTime.UtcNow;
            var configs = await dbContext
                .DanbooruAutoPostConfigs.Where(c =>
                    c.BatchId == batchId && c.ScheduledAtUtc.HasValue
                )
                .OrderBy(c => c.ScheduledAtUtc)
                .ToListAsync(cancellationToken);

            if (configs.Count == 0)
            {
                return OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                    "Батч не найден или нет отложенных постов",
                    []
                );
            }

            var timeSlots = GenerateTimeSlots(cron, now, now.AddDays(365));
            var slotIndex = 0;

            foreach (var config in configs)
            {
                if (config.ScheduledAtUtc.HasValue && config.ScheduledAtUtc.Value <= now)
                {
                    continue;
                }

                if (slotIndex < timeSlots.Count)
                {
                    config.ScheduledAtUtc = timeSlots[slotIndex];
                    config.UpdatedAtUtc = now;
                    slotIndex++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var dtos = configs.Select(MapToDto).ToList();
            result = OperationResult<List<DanbooruAutoPostConfigDto>>.Ok(
                $"Перепланировано {slotIndex} постов",
                dtos
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка перепланирования батча {BatchId}", batchId);
            result = OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                $"Ошибка: {ex.Message}",
                []
            );
        }

        return result;
    }

    public async Task<OperationResult> DeleteBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult.Bad("Не удалось удалить батч");

        if (batchId == Guid.Empty)
        {
            return OperationResult.Bad("BatchId не может быть пустым");
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var configs = await dbContext
                .DanbooruAutoPostConfigs.Where(c => c.BatchId == batchId)
                .ToListAsync(cancellationToken);

            if (configs.Count == 0)
            {
                return OperationResult.Bad("Батч не найден");
            }

            dbContext.DanbooruAutoPostConfigs.RemoveRange(configs);
            await dbContext.SaveChangesAsync(cancellationToken);

            result = OperationResult.Ok($"Удалено {configs.Count} постов");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка удаления батча {BatchId}", batchId);
            result = OperationResult.Bad($"Ошибка: {ex.Message}");
        }

        return result;
    }

    private static List<DateTime> GenerateTimeSlots(
        CronExpression cron,
        DateTime start,
        DateTime end
    )
    {
        var slots = new List<DateTime>();
        var current = start;

        while (current < end && slots.Count < 10_000)
        {
            var next = cron.GetNextOccurrence(current);
            if (next.HasValue && next.Value <= end)
            {
                slots.Add(next.Value);
                current = next.Value.AddSeconds(1);
            }
            else
            {
                break;
            }
        }

        return slots;
    }

    private async Task<List<DanbooruPost>> FetchUniqueImagesAsync(
        string tags,
        int needed,
        string channelKey,
        CancellationToken cancellationToken
    )
    {
        var uniqueImages = new List<DanbooruPost>();
        var seenIds = new HashSet<int>();
        var maxRetries = 5;
        var batchSize = Math.Min(needed * 2, 200);

        for (var attempt = 0; attempt < maxRetries && uniqueImages.Count < needed; attempt++)
        {
            var posts = await danbooruService.GetRandomPostAsync(tags, batchSize);
            if (posts is null || posts.Length == 0)
            {
                break;
            }

            foreach (var post in posts)
            {
                if (uniqueImages.Count >= needed)
                {
                    break;
                }

                if (seenIds.Contains(post.Id))
                {
                    continue;
                }

                var isDuplicate = await deduplicationService.IsAlreadyPostedAsync(
                    Source,
                    post.Id,
                    channelKey,
                    cancellationToken
                );

                if (!isDuplicate)
                {
                    seenIds.Add(post.Id);
                    uniqueImages.Add(post);
                }
            }
        }

        return uniqueImages;
    }

    private static string GetChannelKey(
        TargetPlatform platform,
        ulong discordChannelId,
        long? telegramChannelId
    )
    {
        return platform == TargetPlatform.Telegram
            ? $"tg_{telegramChannelId}"
            : $"dc_{discordChannelId}";
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

        if (
            request
            is { TargetPlatform: TargetPlatform.Discord, DiscordChannelId: 0 }
                or { TargetPlatform: TargetPlatform.Telegram, TelegramChannelId: null or 0 }
        )
        {
            return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                "Канал не указан для выбранной платформы",
                new DanbooruAutoPostConfigDto()
            );
        }

        if (!string.IsNullOrWhiteSpace(request.CronExpression))
        {
            try
            {
                CronExpression.Parse(request.CronExpression);
            }
            catch (CronFormatException)
            {
                return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                    "Некорректное CRON выражение",
                    new DanbooruAutoPostConfigDto()
                );
            }
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
                entity.TargetPlatform = request.TargetPlatform;
                entity.DiscordChannelId =
                    request.TargetPlatform == TargetPlatform.Discord ? request.DiscordChannelId : 0;
                entity.TelegramChannelId =
                    request.TargetPlatform == TargetPlatform.Telegram
                        ? request.TelegramChannelId
                        : null;
                entity.Tags = request.Tags.Trim();
                entity.CronExpression = request.CronExpression?.Trim() ?? "";
                entity.ScheduledAtUtc = request.ScheduledAtUtc;
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

            if (config is not null)
            {
                await PostImageAsync(config, cancellationToken);

                var entity = await dbContext.DanbooruAutoPostConfigs.FindAsync(
                    [id],
                    cancellationToken
                );
                if (entity is not null)
                {
                    entity.LastExecutedAtUtc = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                result = OperationResult.Ok("Изображение отправлено");
            }
            else
            {
                result = OperationResult.Bad("Конфигурация не найдена");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка ручного триггера DanbooruAutoPost {Id}", id);
            result = OperationResult.Bad($"Ошибка: {ex.Message}");
        }

        return result;
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
                                Id = channel.Id,
                                Name = channel.Name,
                                GuildId = guild.Id,
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

    public async Task<OperationResult<List<TelegramChannelOptionDto>>> GetTelegramChannelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await telegramDiscordBridgeService.GetTelegramChannelsAsync(cancellationToken);
    }

    private static string? ValidateCreateRequest(DanbooruAutoPostCreateRequest request)
    {
        if (request.TargetPlatform == TargetPlatform.Discord && request.DiscordChannelId == 0)
        {
            return "DiscordChannelId обязателен для Discord";
        }

        if (
            request.TargetPlatform == TargetPlatform.Telegram
            && request.TelegramChannelId is null or 0
        )
        {
            return "TelegramChannelId обязателен для Telegram";
        }

        if (request.ScheduledAtUtc.HasValue && request.ScheduledAtUtc.Value <= DateTime.UtcNow)
        {
            return "Дата отложенной публикации должна быть в будущем";
        }

        if (!request.ScheduledAtUtc.HasValue && string.IsNullOrWhiteSpace(request.CronExpression))
        {
            return "Укажите CRON выражение или дату отложенной публикации";
        }

        if (!string.IsNullOrWhiteSpace(request.CronExpression))
        {
            try
            {
                CronExpression.Parse(request.CronExpression);
            }
            catch (CronFormatException)
            {
                return "Некорректное CRON выражение";
            }
        }

        return null;
    }

    private static DanbooruAutoPostConfigDto MapToDto(DanbooruAutoPostConfig entity)
    {
        return new DanbooruAutoPostConfigDto
        {
            Id = entity.Id,
            BatchId = entity.BatchId,
            DanbooruPostId = entity.DanbooruPostId,
            TargetPlatform = entity.TargetPlatform,
            DiscordChannelId = entity.DiscordChannelId,
            TelegramChannelId = entity.TelegramChannelId,
            Tags = entity.Tags,
            CronExpression = entity.CronExpression,
            ScheduledAtUtc = entity.ScheduledAtUtc,
            IsEnabled = entity.IsEnabled,
            LastExecutedAtUtc = entity.LastExecutedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }
}
