using System;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.BooruShared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.BooruShared;

public class DeduplicationService(
    ILogger<DeduplicationService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory
) : IDeduplicationService
{
    public async Task<bool> IsAlreadyPostedAsync(
        string source,
        int imageId,
        ulong discordChannelId,
        CancellationToken cancellationToken = default
    )
    {
        var result = false;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            result = await dbContext
                .PostedImageRecords.AsNoTracking()
                .AnyAsync(
                    r =>
                        r.Source == source
                        && r.ImageId == imageId
                        && r.DiscordChannelId == discordChannelId,
                    cancellationToken
                );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка проверки дубликата изображения {Source}:{ImageId} в канале {ChannelId}",
                source,
                imageId,
                discordChannelId
            );
        }

        return result;
    }

    public async Task RecordPostAsync(
        string source,
        int imageId,
        ulong discordChannelId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var record = new PostedImageRecord
            {
                Source = source,
                ImageId = imageId,
                DiscordChannelId = discordChannelId,
                PostedAtUtc = DateTime.UtcNow,
            };

            dbContext.PostedImageRecords.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка записи дубликата изображения {Source}:{ImageId} в канале {ChannelId}",
                source,
                imageId,
                discordChannelId
            );
        }
    }

    public async Task<bool> IsAlreadyPostedAsync(
        string source,
        int imageId,
        string channelKey,
        CancellationToken cancellationToken = default
    )
    {
        var result = false;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            result = await dbContext
                .PostedImageRecords.AsNoTracking()
                .AnyAsync(
                    r => r.Source == source && r.ImageId == imageId && r.ChannelKey == channelKey,
                    cancellationToken
                );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка проверки дубликата изображения {Source}:{ImageId} в канале {ChannelKey}",
                source,
                imageId,
                channelKey
            );
        }

        return result;
    }

    public async Task RecordPostAsync(
        string source,
        int imageId,
        string channelKey,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var record = new PostedImageRecord
            {
                Source = source,
                ImageId = imageId,
                ChannelKey = channelKey,
                PostedAtUtc = DateTime.UtcNow,
            };

            dbContext.PostedImageRecords.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка записи дубликата изображения {Source}:{ImageId} в канале {ChannelKey}",
                source,
                imageId,
                channelKey
            );
        }
    }
}
