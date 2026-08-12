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
    ITelegramBotClient telegramBotClient
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
        var channelId = GetChannelId(config);

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

    private static ulong GetChannelId(DanbooruAutoPostConfig config)
    {
        return config.TargetPlatform == TargetPlatform.Telegram
            ? (ulong)Math.Abs(config.TelegramChannelId ?? 0)
            : config.DiscordChannelId;
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
            TargetPostCount = entity.TargetPostCount,
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
