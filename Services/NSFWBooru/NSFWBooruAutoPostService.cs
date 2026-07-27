using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.BooruShared;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.NSFWBooru.Entities;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.NSFWBooru;

public class NSFWBooruAutoPostService(
    ILogger<NSFWBooruAutoPostService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    Rule34RandomPostService rule34Service,
    IDiscordGatewayService discordGatewayService,
    IHttpClientFactory httpClientFactory,
    IDeduplicationService deduplicationService
) : BackgroundService, INSFWBooruAutoPostService
{
    private const string Source = "Rule34";
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
                logger.LogError(ex, "Ошибка в цикле NSFWBooruAutoPostService");
            }
        }
    }

    private async Task ProcessScheduledPostsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var configs = await dbContext
            .NSFWBooruAutoPostConfigs.AsNoTracking()
            .Where(c => c.IsEnabled)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var config in configs)
        {
            try
            {
                var cron = CronExpression.Parse(config.CronExpression);
                var nextOccurrence = config.LastExecutedAtUtc.HasValue
                    ? cron.GetNextOccurrence(config.LastExecutedAtUtc.Value)
                    : cron.GetNextOccurrence(now.AddMinutes(-1));

                if (nextOccurrence.HasValue && nextOccurrence.Value <= now)
                {
                    await PostImageAsync(config, cancellationToken);

                    await using var updateContext = await dbContextFactory.CreateDbContextAsync(
                        cancellationToken
                    );
                    var entity = await updateContext.NSFWBooruAutoPostConfigs.FindAsync(
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
        NSFWBooruAutoPostConfig config,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 0; attempt <= MaxDedupRetries; attempt++)
        {
            var posts = await rule34Service.GetRandomPostAsync(config.Tags, 1);
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

            var fileUrl = post.FileUrl ?? post.SampleUrl;
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                logger.LogWarning(
                    "Пост {PostId} не имеет URL файла для конфига {ConfigId}",
                    post.Id,
                    config.Id
                );
                return;
            }

            using var httpClient = httpClientFactory.CreateClient();
            using var response = await httpClient.GetAsync(fileUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = Path.GetFileName(new Uri(fileUrl).AbsolutePath);
            var tagPreview = post
                .Tags?.Split(' ')
                .Take(5)
                .Aggregate("", (a, b) => $"{a} {b}")
                .Trim();

            var message =
                $"**Rule34** | Score: {post.Score} | Rating: {post.Rating}\n"
                + $"Tags: {tagPreview}\n"
                + $"https://rule34.xxx/index.php?page=post&s=view&id={post.Id}";

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

    public async Task<OperationResult<List<NSFWBooruAutoPostConfigDto>>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<NSFWBooruAutoPostConfigDto>>.Bad(
            "Не удалось получить конфигурации",
            []
        );

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var configs = await dbContext
                .NSFWBooruAutoPostConfigs.AsNoTracking()
                .OrderBy(c => c.DiscordChannelId)
                .ThenBy(c => c.CreatedAtUtc)
                .Select(c => new NSFWBooruAutoPostConfigDto
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

            result = OperationResult<List<NSFWBooruAutoPostConfigDto>>.Ok(
                "Конфигурации получены",
                configs
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения конфигураций NSFWBooruAutoPost");
            result = OperationResult<List<NSFWBooruAutoPostConfigDto>>.Bad(ex.Message, []);
        }

        return result;
    }

    public async Task<OperationResult<NSFWBooruAutoPostConfigDto>> CreateAsync(
        NSFWBooruAutoPostCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
            "Не удалось создать конфигурацию",
            new NSFWBooruAutoPostConfigDto()
        );

        if (request.DiscordChannelId == 0)
        {
            return OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                "DiscordChannelId обязателен",
                new NSFWBooruAutoPostConfigDto()
            );
        }

        if (string.IsNullOrWhiteSpace(request.CronExpression))
        {
            return OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                "CRON выражение обязательно",
                new NSFWBooruAutoPostConfigDto()
            );
        }

        try
        {
            CronExpression.Parse(request.CronExpression);
        }
        catch (CronFormatException)
        {
            return OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                "Некорректное CRON выражение",
                new NSFWBooruAutoPostConfigDto()
            );
        }

        var tagValidationError = TagValidator.GetValidationError(request.Tags);
        if (tagValidationError is not null)
        {
            return OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                tagValidationError,
                new NSFWBooruAutoPostConfigDto()
            );
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var now = DateTime.Now;
            var entity = new NSFWBooruAutoPostConfig
            {
                DiscordChannelId = request.DiscordChannelId,
                Tags = request.Tags.Trim(),
                CronExpression = request.CronExpression.Trim(),
                IsEnabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            dbContext.NSFWBooruAutoPostConfigs.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            result = OperationResult<NSFWBooruAutoPostConfigDto>.Ok(
                "Конфигурация создана",
                new NSFWBooruAutoPostConfigDto
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
            logger.LogError(ex, "Ошибка создания конфигурации NSFWBooruAutoPost");
            result = OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                $"Ошибка создания: {ex.Message}",
                new NSFWBooruAutoPostConfigDto()
            );
        }

        return result;
    }

    public async Task<OperationResult<NSFWBooruAutoPostConfigDto>> UpdateAsync(
        NSFWBooruAutoPostUpdateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
            "Не удалось обновить конфигурацию",
            new NSFWBooruAutoPostConfigDto()
        );

        if (request.Id == Guid.Empty)
        {
            return OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                "Id не может быть пустым",
                new NSFWBooruAutoPostConfigDto()
            );
        }

        try
        {
            CronExpression.Parse(request.CronExpression);
        }
        catch (CronFormatException)
        {
            return OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                "Некорректное CRON выражение",
                new NSFWBooruAutoPostConfigDto()
            );
        }

        var tagValidationError = TagValidator.GetValidationError(request.Tags);
        if (tagValidationError is not null)
        {
            return OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                tagValidationError,
                new NSFWBooruAutoPostConfigDto()
            );
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var entity = await dbContext.NSFWBooruAutoPostConfigs.FindAsync(
                [request.Id],
                cancellationToken
            );

            if (entity is not null)
            {
                entity.DiscordChannelId = request.DiscordChannelId;
                entity.Tags = request.Tags.Trim();
                entity.CronExpression = request.CronExpression.Trim();
                entity.UpdatedAtUtc = DateTime.Now;
                await dbContext.SaveChangesAsync(cancellationToken);

                result = OperationResult<NSFWBooruAutoPostConfigDto>.Ok(
                    "Конфигурация обновлена",
                    new NSFWBooruAutoPostConfigDto
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
                result = OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                    "Конфигурация не найдена",
                    new NSFWBooruAutoPostConfigDto()
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка обновления конфигурации NSFWBooruAutoPost {Id}",
                request.Id
            );
            result = OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                $"Ошибка обновления: {ex.Message}",
                new NSFWBooruAutoPostConfigDto()
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
            var entity = await dbContext.NSFWBooruAutoPostConfigs.FindAsync(
                [id],
                cancellationToken
            );

            if (entity is not null)
            {
                dbContext.NSFWBooruAutoPostConfigs.Remove(entity);
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
            logger.LogError(ex, "Ошибка удаления конфигурации NSFWBooruAutoPost {Id}", id);
            result = OperationResult.Bad($"Ошибка удаления: {ex.Message}");
        }

        return result;
    }

    public async Task<OperationResult<NSFWBooruAutoPostConfigDto>> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
            "Не удалось изменить состояние",
            new NSFWBooruAutoPostConfigDto()
        );

        if (id == Guid.Empty)
        {
            return OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                "Id не может быть пустым",
                new NSFWBooruAutoPostConfigDto()
            );
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var entity = await dbContext.NSFWBooruAutoPostConfigs.FindAsync(
                [id],
                cancellationToken
            );

            if (entity is not null)
            {
                entity.IsEnabled = isEnabled;
                entity.UpdatedAtUtc = DateTime.Now;
                await dbContext.SaveChangesAsync(cancellationToken);

                result = OperationResult<NSFWBooruAutoPostConfigDto>.Ok(
                    "Состояние обновлено",
                    new NSFWBooruAutoPostConfigDto
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
                result = OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                    "Конфигурация не найдена",
                    new NSFWBooruAutoPostConfigDto()
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка изменения состояния NSFWBooruAutoPost {Id}", id);
            result = OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                $"Ошибка: {ex.Message}",
                new NSFWBooruAutoPostConfigDto()
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
                .NSFWBooruAutoPostConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (config is not null)
            {
                await PostImageAsync(config, cancellationToken);

                var entity = await dbContext.NSFWBooruAutoPostConfigs.FindAsync(
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
            logger.LogError(ex, "Ошибка ручного триггера NSFWBooruAutoPost {Id}", id);
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
            logger.LogError(ex, "Ошибка получения Discord каналов для NSFWBooruAutoPost");
            result = OperationResult<List<DiscordChannelOptionDto>>.Bad(
                $"Ошибка: {ex.Message}",
                []
            );
        }

        return result;
    }
}
