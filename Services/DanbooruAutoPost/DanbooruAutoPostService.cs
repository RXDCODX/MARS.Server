using Cronos;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.BooruShared;
using MARS.Server.Services.DanbooruAutoPost.Entities;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using MARS.Server.Services.Twitch.Rewards._27_RandomArt;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.DanbooruAutoPost;

public class DanbooruAutoPostService(
    ILogger<DanbooruAutoPostService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    DanbooruRandomPostService danbooruService,
    IDiscordGatewayService discordGatewayService,
    IDeduplicationService deduplicationService
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
                var cron = CronExpression.Parse(config.CronExpression);
                var lastExecuted = config.LastExecutedAtUtc.HasValue
                    ? DateTime.SpecifyKind(config.LastExecutedAtUtc.Value, DateTimeKind.Utc)
                    : (DateTime?)null;

                var nextOccurrence = lastExecuted.HasValue
                    ? cron.GetNextOccurrence(lastExecuted.Value)
                    : cron.GetNextOccurrence(now.AddMinutes(-1));

                if (nextOccurrence.HasValue && nextOccurrence.Value <= now)
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
                    "Ошибка отправки изображения для конфига {ConfigId} в канал {ChannelId}",
                    config.Id,
                    config.DiscordChannelId
                );
            }
        }
    }

    private async Task PostImageAsync(
        DanbooruAutoPostConfig config,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 0; attempt <= MaxDedupRetries; attempt++)
        {
            var posts = await danbooruService.GetRandomPostAsync(config.Tags, 1);
            if (posts is null || posts.Length == 0)
            {
                logger.LogWarning(
                    "Не найдено постов по тегам '{Tags}' для конфига {ConfigId}",
                    config.Tags,
                    config.Id
                );
                return;
            }

            var post = posts[0];

            if (
                await deduplicationService.IsAlreadyPostedAsync(
                    Source,
                    post.Id,
                    config.DiscordChannelId,
                    cancellationToken
                )
            )
            {
                logger.LogInformation(
                    "Изображение {PostId} уже отправлено в канал {ChannelId}, попытка {Attempt}/{Max}",
                    post.Id,
                    config.DiscordChannelId,
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

            var message =
                $"**Danbooru** | Score: {post.Score} | Rating: {post.Rating}\n"
                + $"Tags: {tagPreview}\n"
                + $"https://danbooru.donmai.us/posts/{post.Id}";

            await using var stream = new MemoryStream(fileBytes);
            var result = await discordGatewayService.SendFileAsync(
                config.DiscordChannelId,
                stream,
                fileName,
                message,
                cancellationToken
            );

            if (result.Success)
            {
                await deduplicationService.RecordPostAsync(
                    Source,
                    post.Id,
                    config.DiscordChannelId,
                    cancellationToken
                );
            }
            else
            {
                logger.LogWarning(
                    "Не удалось отправить изображение в Discord канал {ChannelId}: {Error}",
                    config.DiscordChannelId,
                    result.Message
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
                .OrderBy(c => c.DiscordChannelId)
                .ThenBy(c => c.CreatedAtUtc)
                .Select(c => new DanbooruAutoPostConfigDto
                {
                    Id = c.Id,
                    DiscordChannelId = c.DiscordChannelId,
                    Tags = c.Tags,
                    CronExpression = c.CronExpression,
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

        if (request.DiscordChannelId == 0)
        {
            return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                "DiscordChannelId обязателен",
                new DanbooruAutoPostConfigDto()
            );
        }

        if (string.IsNullOrWhiteSpace(request.CronExpression))
        {
            return OperationResult<DanbooruAutoPostConfigDto>.Bad(
                "CRON выражение обязательно",
                new DanbooruAutoPostConfigDto()
            );
        }

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
                DiscordChannelId = request.DiscordChannelId,
                Tags = request.Tags.Trim(),
                CronExpression = request.CronExpression.Trim(),
                IsEnabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            dbContext.DanbooruAutoPostConfigs.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            result = OperationResult<DanbooruAutoPostConfigDto>.Ok(
                "Конфигурация создана",
                new DanbooruAutoPostConfigDto
                {
                    Id = entity.Id,
                    DiscordChannelId = entity.DiscordChannelId,
                    Tags = entity.Tags,
                    CronExpression = entity.CronExpression,
                    IsEnabled = entity.IsEnabled,
                    LastExecutedAtUtc = entity.LastExecutedAtUtc,
                    CreatedAtUtc = entity.CreatedAtUtc,
                    UpdatedAtUtc = entity.UpdatedAtUtc,
                }
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
                entity.DiscordChannelId = request.DiscordChannelId;
                entity.Tags = request.Tags.Trim();
                entity.CronExpression = request.CronExpression.Trim();
                entity.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                result = OperationResult<DanbooruAutoPostConfigDto>.Ok(
                    "Конфигурация обновлена",
                    new DanbooruAutoPostConfigDto
                    {
                        Id = entity.Id,
                        DiscordChannelId = entity.DiscordChannelId,
                        Tags = entity.Tags,
                        CronExpression = entity.CronExpression,
                        IsEnabled = entity.IsEnabled,
                        LastExecutedAtUtc = entity.LastExecutedAtUtc,
                        CreatedAtUtc = entity.CreatedAtUtc,
                        UpdatedAtUtc = entity.UpdatedAtUtc,
                    }
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
                    new DanbooruAutoPostConfigDto
                    {
                        Id = entity.Id,
                        DiscordChannelId = entity.DiscordChannelId,
                        Tags = entity.Tags,
                        CronExpression = entity.CronExpression,
                        IsEnabled = entity.IsEnabled,
                        LastExecutedAtUtc = entity.LastExecutedAtUtc,
                        CreatedAtUtc = entity.CreatedAtUtc,
                        UpdatedAtUtc = entity.UpdatedAtUtc,
                    }
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
}
