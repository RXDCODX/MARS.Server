using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest.Queue;

public class SoundRequestUserQueue(
    IDbContextFactory<AppDbContext> contextFactory,
    IHostApplicationLifetime lifetime
)
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    /// <summary>
    /// Добавить трек в очередь
    /// </summary>
    public async Task<QueueItem> AddToQueueAsync(
        BaseTrackInfo track,
        string requestedByTwitchId,
        TwitchUser requestedByTwitchUser
    )
    {
        QueueItem result = null!;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        if (!string.IsNullOrWhiteSpace(requestedByTwitchId))
        {
            // Сначала убедимся, что трек существует в базе, или создадим новый
            // Проверяем сначала по VideoId (если есть), затем по URL
            BaseTrackInfo? existingTrack = null;

            if (!string.IsNullOrWhiteSpace(track.VideoId))
            {
                existingTrack = await dbContext
                    .SoundRequestBaseTrackInfos.AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => t.VideoId == track.VideoId && !t.IsDeleted,
                        cancellationToken: _cancellationToken
                    );
            }

            // Если не нашли по VideoId, проверяем по URL
            if (existingTrack == null)
            {
                existingTrack = await dbContext
                    .SoundRequestBaseTrackInfos.AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => t.Url == track.Url && !t.IsDeleted,
                        cancellationToken: _cancellationToken
                    );
            }

            Guid trackId;
            if (existingTrack != null)
            {
                trackId = existingTrack.Id;
                // Обновляем данные существующего трека
                existingTrack.TrackName = track.TrackName;
                existingTrack.Authors = track.Authors;
                existingTrack.Duration = track.Duration;
                existingTrack.ArtworkUrl = track.ArtworkUrl;
                existingTrack.VideoId = track.VideoId;
                existingTrack.UpdatedAt = DateTime.UtcNow;
                dbContext.SoundRequestBaseTrackInfos.Update(existingTrack);
            }
            else
            {
                // Создаем новый трек
                track.IsDeleted = false;
                dbContext.SoundRequestBaseTrackInfos.Add(track);
                await dbContext.SaveChangesAsync(_cancellationToken);
                trackId = track.Id;
            }

            // Получаем максимальный порядок в очереди
            var maxOrder =
                await dbContext
                    .SoundRequestQueueItems.AsNoTracking()
                    .Where(qi => qi.QueueOrder != null)
                    .MaxAsync(qi => (int?)qi.QueueOrder, cancellationToken: _cancellationToken)
                ?? -1;

            // Создаем элемент очереди
            var queueItem = new QueueItem
            {
                TrackId = trackId,
                Track = existingTrack ?? track,
                QueueOrder = maxOrder + 1,
                RequestedByTwitchId = requestedByTwitchId,
                RequestedAt = DateTime.UtcNow,
                IsDeleted = false,
            };

            dbContext.SoundRequestQueueItems.Add(queueItem);
            await dbContext.SaveChangesAsync(_cancellationToken);

            result = queueItem;
        }

        return result;
    }

    /// <summary>
    /// Удалить элемент из очереди (помечает как удаленный)
    /// </summary>
    public async Task RemoveFromQueueAsync(Guid queueItemId)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        var queueItemToRemove = await dbContext.SoundRequestQueueItems.FindAsync(
            [queueItemId],
            cancellationToken: _cancellationToken
        );

        if (queueItemToRemove != null)
        {
            var removedOrder = queueItemToRemove.QueueOrder;

            // Помечаем как удаленный
            queueItemToRemove.IsDeleted = true;
            queueItemToRemove.QueueOrder = null;
            dbContext.SoundRequestQueueItems.Update(queueItemToRemove);

            // Обновляем порядок остальных элементов в очереди
            if (removedOrder.HasValue)
            {
                await dbContext
                    .SoundRequestQueueItems.Where(qi =>
                        qi.QueueOrder > removedOrder.Value && !qi.IsDeleted
                    )
                    .ExecuteUpdateAsync(
                        e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder - 1),
                        cancellationToken: _cancellationToken
                    );
            }

            await dbContext.SaveChangesAsync(_cancellationToken);
        }
    }

    /// <summary>
    /// Получить очередь элементов (только не удаленные)
    /// </summary>
    public async Task<List<QueueItem>> GetQueueAsync()
    {
        List<QueueItem> result = [];

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        result = await dbContext
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .Where(qi => !qi.IsDeleted && qi.QueueOrder != null)
            .OrderBy(qi => qi.QueueOrder)
            .ToListAsync(cancellationToken: _cancellationToken);

        return result;
    }

    /// <summary>
    /// Получить следующий элемент из очереди
    /// </summary>
    public async Task<QueueItem?> GetNextQueueItemAsync()
    {
        QueueItem? result = null;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        result = await dbContext
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .Where(qi => !qi.IsDeleted && qi.QueueOrder != null)
            .OrderBy(qi => qi.QueueOrder)
            .FirstOrDefaultAsync(cancellationToken: _cancellationToken);

        return result;
    }

    /// <summary>
    /// Получить количество элементов в очереди
    /// </summary>
    public async Task<int> GetQueueCountAsync()
    {
        var result = 0;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        result = await dbContext.SoundRequestQueueItems.CountAsync(
            qi => !qi.IsDeleted && qi.QueueOrder != null,
            cancellationToken: _cancellationToken
        );

        return result;
    }

    /// <summary>
    /// Получить элемент очереди по ID
    /// </summary>
    public async Task<QueueItem?> GetQueueItemByIdAsync(Guid queueItemId)
    {
        QueueItem? result = null;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        result = await dbContext
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .FirstOrDefaultAsync(qi => qi.Id == queueItemId, cancellationToken: _cancellationToken);

        return result;
    }

    /// <summary>
    /// Получить элементы очереди пользователя
    /// </summary>
    public async Task<List<QueueItem>> GetUserQueueItemsAsync(string twitchId)
    {
        List<QueueItem> result = [];

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        if (!string.IsNullOrWhiteSpace(twitchId))
        {
            result = await dbContext
                .SoundRequestQueueItems.AsNoTracking()
                .Include(qi => qi.Track)
                .Include(qi => qi.RequestedByTwitchUser)
                .Where(qi =>
                    !qi.IsDeleted && qi.QueueOrder != null && qi.RequestedByTwitchId == twitchId
                )
                .OrderBy(qi => qi.QueueOrder)
                .ToListAsync(cancellationToken: _cancellationToken);
        }

        return result;
    }
}
