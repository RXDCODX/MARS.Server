using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest.Queue;

public class SoundRequestUserQueue(
    IDbContextFactory<AppDbContext> contextFactory,
    IHostApplicationLifetime lifetime,
    TwitchUserEnsureService twitchUserEnsureService
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
                        t => t.VideoId == track.VideoId,
                        cancellationToken: _cancellationToken
                    );
            }

            // Если не нашли по VideoId, проверяем по URL
            existingTrack ??= await dbContext
                .SoundRequestBaseTrackInfos.AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.Url == track.Url,
                    cancellationToken: _cancellationToken
                );

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
                dbContext.SoundRequestBaseTrackInfos.Add(track);
                await dbContext.SaveChangesAsync(_cancellationToken);
                trackId = track.Id;
            }

            // Гарантируем наличие пользователя в TwitchUsers перед созданием QueueItem
            await twitchUserEnsureService.EnsureUserExistsAsync(
                requestedByTwitchId,
                cancellationToken: _cancellationToken
            );

            // Получаем максимальный QueueOrder среди элементов очереди (>= 0)
            var isQueueItemsExists = await dbContext
                .SoundRequestQueueItems.Where(e => e.QueueOrder >= 0)
                .AsNoTracking()
                .AnyAsync(cancellationToken: _cancellationToken);

            var maxOrder = isQueueItemsExists
                ? await dbContext
                    .SoundRequestQueueItems.AsNoTracking()
                    .Where(e => e.QueueOrder >= 0)
                    .MaxAsync(e => e.QueueOrder, cancellationToken: _cancellationToken)
                : -1;

            // Создаем элемент очереди с QueueOrder = maxOrder + 1
            var queueItem = new QueueItem
            {
                TrackId = trackId,
                Track = existingTrack ?? track,
                QueueOrder = maxOrder + 1,
                RequestedByTwitchId = requestedByTwitchId,
                RequestedAt = DateTime.Now,
            };

            dbContext.SoundRequestQueueItems.Add(queueItem);
            await dbContext.SaveChangesAsync(_cancellationToken);

            queueItem.RequestedByTwitchUser = requestedByTwitchUser;

            result = queueItem;
        }

        return result;
    }

    /// <summary>
    /// Удалить элемент из очереди (физическое удаление из БД)
    /// Если удаляемый элемент был в очереди (QueueOrder > removedOrder), сдвигаем порядок остальных на -1
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

            // Физически удаляем элемент из БД
            dbContext.SoundRequestQueueItems.Remove(queueItemToRemove);

            // Сдвигаем порядок остальных элементов в очереди (только те, что были после удаленного)
            try
            {
                await dbContext
                    .SoundRequestQueueItems.Where(qi => qi.QueueOrder > removedOrder)
                    .ExecuteUpdateAsync(
                        e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder - 1),
                        cancellationToken: _cancellationToken
                    );
            }
            catch (InvalidOperationException)
            {
                var affectedItems = await dbContext
                    .SoundRequestQueueItems.Where(qi => qi.QueueOrder > removedOrder)
                    .ToListAsync(cancellationToken: _cancellationToken);

                foreach (var affectedItem in affectedItems)
                {
                    affectedItem.QueueOrder -= 1;
                }
            }

            await dbContext.SaveChangesAsync(_cancellationToken);
        }
    }

    /// <summary>
    /// Полностью очистить очередь (все элементы с QueueOrder >= 0)
    /// История (QueueOrder < 0) не затрагивается.
    /// </summary>
    public async Task<int> ClearQueueAsync()
    {
        var result = 0;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        var queueItemsQuery = dbContext.SoundRequestQueueItems.Where(qi => qi.QueueOrder >= 0);

        try
        {
            result = await queueItemsQuery.ExecuteDeleteAsync(cancellationToken: _cancellationToken);
        }
        catch (InvalidOperationException)
        {
            var queueItems = await queueItemsQuery.ToListAsync(cancellationToken: _cancellationToken);

            result = queueItems.Count;
            dbContext.SoundRequestQueueItems.RemoveRange(queueItems);
            await dbContext.SaveChangesAsync(_cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Получить очередь элементов (QueueOrder >= 0)
    /// </summary>
    public async Task<List<QueueItem>> GetQueueAsync()
    {
        List<QueueItem> result = [];

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        result = await dbContext
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .Where(qi => qi.QueueOrder >= 0)
            .OrderBy(qi => qi.QueueOrder)
            .ToListAsync(cancellationToken: _cancellationToken);

        return result;
    }

    /// <summary>
    /// Получить текущий элемент очереди (с QueueOrder = 0)
    /// </summary>
    public async Task<QueueItem?> GetCurrentQueueItemAsync()
    {
        QueueItem? result = null;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        result = await dbContext
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .Where(qi => qi.QueueOrder == 0)
            .FirstOrDefaultAsync(cancellationToken: _cancellationToken);

        return result;
    }

    /// <summary>
    /// Получить следующий элемент из очереди (с QueueOrder = 1)
    /// </summary>
    public async Task<QueueItem?> GetNextQueueItemAsync()
    {
        QueueItem? result = null;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        result = await dbContext
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .Where(qi => qi.QueueOrder == 1)
            .FirstOrDefaultAsync(cancellationToken: _cancellationToken);

        return result;
    }

    /// <summary>
    /// Получить количество элементов в очереди (QueueOrder >= 0)
    /// </summary>
    public async Task<int> GetQueueCountAsync()
    {
        var result = 0;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        result = await dbContext.SoundRequestQueueItems.CountAsync(
            qi => qi.QueueOrder >= 0,
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
    /// Добавить трек в начало очереди
    /// </summary>
    public async Task<QueueItem> AddToQueueFrontAsync(
        BaseTrackInfo track,
        string requestedByTwitchId,
        TwitchUser requestedByTwitchUser
    )
    {
        QueueItem result = null!;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        if (!string.IsNullOrWhiteSpace(requestedByTwitchId))
        {
            // Сдвигаем все существующие элементы очереди на 1 позицию вверх (только QueueOrder >= 1)
            try
            {
                await dbContext
                    .SoundRequestQueueItems.Where(qi => qi.QueueOrder >= 1)
                    .ExecuteUpdateAsync(
                        e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder + 1),
                        cancellationToken: _cancellationToken
                    );
            }
            catch (InvalidOperationException)
            {
                var queueItems = await dbContext
                    .SoundRequestQueueItems.Where(qi => qi.QueueOrder >= 1)
                    .ToListAsync(cancellationToken: _cancellationToken);

                foreach (var item in queueItems)
                {
                    item.QueueOrder += 1;
                }
            }

            // Сначала убедимся, что трек существует в базе, или создадим новый
            BaseTrackInfo? existingTrack = null;

            if (!string.IsNullOrWhiteSpace(track.VideoId))
            {
                existingTrack = await dbContext
                    .SoundRequestBaseTrackInfos.AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => t.VideoId == track.VideoId,
                        cancellationToken: _cancellationToken
                    );
            }

            existingTrack ??= await dbContext
                .SoundRequestBaseTrackInfos.AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.Url == track.Url,
                    cancellationToken: _cancellationToken
                );

            Guid trackId;
            if (existingTrack != null)
            {
                trackId = existingTrack.Id;
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
                dbContext.SoundRequestBaseTrackInfos.Add(track);
                await dbContext.SaveChangesAsync(_cancellationToken);
                trackId = track.Id;
            }

            // Гарантируем наличие пользователя в TwitchUsers
            await twitchUserEnsureService.EnsureUserExistsAsync(
                requestedByTwitchId,
                cancellationToken: _cancellationToken
            );

            // Создаем элемент очереди с QueueOrder = 1 (первый в очереди)
            var queueItem = new QueueItem
            {
                TrackId = trackId,
                Track = existingTrack ?? track,
                QueueOrder = 1,
                RequestedByTwitchId = requestedByTwitchId,
                RequestedAt = DateTime.Now,
            };

            dbContext.SoundRequestQueueItems.Add(queueItem);
            await dbContext.SaveChangesAsync(_cancellationToken);

            queueItem.RequestedByTwitchUser = requestedByTwitchUser;

            result = queueItem;
        }

        return result;
    }

    /// <summary>
    /// Получить элементы очереди пользователя (QueueOrder >= 0)
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
                .Where(qi => qi.QueueOrder >= 0 && qi.RequestedByTwitchId == twitchId)
                .OrderBy(qi => qi.QueueOrder)
                .ToListAsync(cancellationToken: _cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Сдвинуть всю очередь на -1 и получить элемент для воспроизведения (с новым QueueOrder = 0)
    /// Используется при начале воспроизведения трека
    /// </summary>
    /// <returns>Элемент для воспроизведения или null, если очередь пуста</returns>
    public async Task<QueueItem?> ShiftQueueAndGetCurrentAsync()
    {
        QueueItem? result = null;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        // Сдвигаем всю очередь на -1 (включая элементы с QueueOrder >= 0)
        try
        {
            await dbContext.SoundRequestQueueItems.ExecuteUpdateAsync(
                e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder - 1),
                cancellationToken: _cancellationToken
            );
        }
        catch (InvalidOperationException)
        {
            var queueItems = await dbContext.SoundRequestQueueItems.ToListAsync(
                cancellationToken: _cancellationToken
            );

            foreach (var queueItem in queueItems)
            {
                queueItem.QueueOrder -= 1;
            }

            await dbContext.SaveChangesAsync(_cancellationToken);
        }

        // Получаем элемент с QueueOrder = 0 (который теперь нужно воспроизвести)
        result = await dbContext
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .Where(qi => qi.QueueOrder == 0)
            .FirstOrDefaultAsync(cancellationToken: _cancellationToken);

        return result;
    }

    /// <summary>
    /// Переместить элемент очереди на позицию 0 (текущий) и запустить немедленно
    /// Текущий трек (QueueOrder = 0) переходит в историю (QueueOrder = -1)
    /// Элементы между новой позицией и 0 сдвигаются вверх на одну позицию
    /// </summary>
    /// <param name="queueItemId">ID элемента для воспроизведения</param>
    /// <returns>Элемент для немедленного воспроизведения или null</returns>
    public async Task<QueueItem?> MoveToFrontAndPlayAsync(Guid queueItemId)
    {
        QueueItem? result = null;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        // Получаем элемент, который нужно переместить
        var itemToMove = await dbContext
            .SoundRequestQueueItems.Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .FirstOrDefaultAsync(qi => qi.Id == queueItemId, cancellationToken: _cancellationToken);

        // Проверяем, что элемент найден и не является уже текущим
        if (itemToMove != null && itemToMove.QueueOrder != 0)
        {
            var itemQueueOrder = itemToMove.QueueOrder;

            if (itemQueueOrder > 0)
            {
                // Сдвигаем всю историю (QueueOrder < 0) на -1
                try
                {
                    await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder < 0)
                        .ExecuteUpdateAsync(
                            e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder - 1),
                            cancellationToken: _cancellationToken
                        );
                }
                catch (InvalidOperationException)
                {
                    var historyItems = await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder < 0)
                        .ToListAsync(cancellationToken: _cancellationToken);

                    foreach (var item in historyItems)
                    {
                        item.QueueOrder -= 1;
                    }
                }

                // Переводим текущий трек (QueueOrder = 0) в историю
                var currentItem = await dbContext.SoundRequestQueueItems.FirstOrDefaultAsync(
                    qi => qi.QueueOrder == 0,
                    cancellationToken: _cancellationToken
                );

                currentItem?.QueueOrder = -1;

                // Сдвигаем все элементы после перемещаемого трека в очереди на -1
                try
                {
                    await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder > itemQueueOrder)
                        .ExecuteUpdateAsync(
                            e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder - 1),
                            cancellationToken: _cancellationToken
                        );
                }
                catch (InvalidOperationException)
                {
                    var affectedItems = await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder > itemQueueOrder)
                        .ToListAsync(cancellationToken: _cancellationToken);

                    foreach (var item in affectedItems)
                    {
                        item.QueueOrder -= 1;
                    }
                }
            }
            else
            {
                // Если выбирают трек из истории, текущий трек становится следующим для воспроизведения
                var currentItem = await dbContext.SoundRequestQueueItems.FirstOrDefaultAsync(
                    qi => qi.QueueOrder == 0,
                    cancellationToken: _cancellationToken
                );

                currentItem?.QueueOrder = 1;

                // Все будущие треки сдвигаем вперед на одну позицию
                try
                {
                    await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder >= 1)
                        .ExecuteUpdateAsync(
                            e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder + 1),
                            cancellationToken: _cancellationToken
                        );
                }
                catch (InvalidOperationException)
                {
                    var affectedItems = await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder >= 1)
                        .ToListAsync(cancellationToken: _cancellationToken);

                    foreach (var item in affectedItems)
                    {
                        item.QueueOrder += 1;
                    }
                }
            }

            // Переводим нужный элемент в позицию 0
            itemToMove.QueueOrder = 0;

            await dbContext.SaveChangesAsync(_cancellationToken);

            // Перезагружаем элемент для возврата (с треком)
            result = await dbContext
                .SoundRequestQueueItems.AsNoTracking()
                .Include(qi => qi.Track)
                .Include(qi => qi.RequestedByTwitchUser)
                .FirstOrDefaultAsync(
                    qi => qi.Id == queueItemId,
                    cancellationToken: _cancellationToken
                );
        }

        return result;
    }

    /// <summary>
    /// Переместить элемент очереди на указанную позицию (включая вставку из истории в очередь)
    /// Поддерживает вставку в любую позицию очереди (newPosition >= 0).
    /// Возвращает перемещённый элемент или null.
    /// </summary>
    public async Task<QueueItem?> MoveQueueItemToPositionAsync(Guid queueItemId, int newPosition)
    {
        QueueItem? result = null;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        var itemToMove = await dbContext
            .SoundRequestQueueItems.Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .FirstOrDefaultAsync(qi => qi.Id == queueItemId, cancellationToken: _cancellationToken);

        if (itemToMove is null)
        {
            return null;
        }

        // Normalize target position
        if (newPosition < 0)
        {
            return null;
        }

        // Count current queue items (QueueOrder >= 0)
        var queueCount = await dbContext.SoundRequestQueueItems.CountAsync(qi => qi.QueueOrder >= 0, cancellationToken: _cancellationToken);

        // If moving from queue -> allowed range: 0..queueCount-1
        // If moving from history -> allowed range: 0..queueCount (inserting at end)
        var oldPos = itemToMove.QueueOrder;
        var maxPos = oldPos >= 0 ? Math.Max(queueCount - 1, 0) : queueCount;
        var targetPos = Math.Min(newPosition, maxPos);

        if (oldPos == targetPos)
        {
            // Nothing to do
            result = itemToMove;
            return result;
        }

        if (oldPos >= 0)
        {
            // Moving an existing queue item
            if (targetPos < oldPos)
            {
                // Shift items in [targetPos, oldPos) up by +1
                try
                {
                    await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder >= targetPos && qi.QueueOrder < oldPos)
                        .ExecuteUpdateAsync(
                            e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder + 1),
                            cancellationToken: _cancellationToken
                        );
                }
                catch (InvalidOperationException)
                {
                    var affected = await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder >= targetPos && qi.QueueOrder < oldPos)
                        .ToListAsync(cancellationToken: _cancellationToken);

                    foreach (var a in affected)
                    {
                        a.QueueOrder += 1;
                    }
                }
            }
            else
            {
                // targetPos > oldPos: shift items in (oldPos, targetPos] down by -1
                try
                {
                    await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder > oldPos && qi.QueueOrder <= targetPos)
                        .ExecuteUpdateAsync(
                            e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder - 1),
                            cancellationToken: _cancellationToken
                        );
                }
                catch (InvalidOperationException)
                {
                    var affected = await dbContext
                        .SoundRequestQueueItems.Where(qi => qi.QueueOrder > oldPos && qi.QueueOrder <= targetPos)
                        .ToListAsync(cancellationToken: _cancellationToken);

                    foreach (var a in affected)
                    {
                        a.QueueOrder -= 1;
                    }
                }
            }
        }
        else
        {
            // Moving from history (oldPos < 0) into queue
            // 1) Close gap in history: items older than oldPos (QueueOrder < oldPos) should +1
            try
            {
                await dbContext
                    .SoundRequestQueueItems.Where(qi => qi.QueueOrder < oldPos)
                    .ExecuteUpdateAsync(
                        e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder + 1),
                        cancellationToken: _cancellationToken
                    );
            }
            catch (InvalidOperationException)
            {
                var historyAffected = await dbContext
                    .SoundRequestQueueItems.Where(qi => qi.QueueOrder < oldPos)
                    .ToListAsync(cancellationToken: _cancellationToken);

                foreach (var a in historyAffected)
                {
                    a.QueueOrder += 1;
                }
            }

            // 2) Make room in queue: shift items with QueueOrder >= targetPos up by +1
            try
            {
                await dbContext
                    .SoundRequestQueueItems.Where(qi => qi.QueueOrder >= targetPos)
                    .ExecuteUpdateAsync(
                        e => e.SetProperty(qi => qi.QueueOrder, qi => qi.QueueOrder + 1),
                        cancellationToken: _cancellationToken
                    );
            }
            catch (InvalidOperationException)
            {
                var queueAffected = await dbContext
                    .SoundRequestQueueItems.Where(qi => qi.QueueOrder >= targetPos)
                    .ToListAsync(cancellationToken: _cancellationToken);

                foreach (var a in queueAffected)
                {
                    a.QueueOrder += 1;
                }
            }
        }

        // Set moved item to target position
        itemToMove.QueueOrder = targetPos;

        await dbContext.SaveChangesAsync(_cancellationToken);

        result = await dbContext
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .FirstOrDefaultAsync(qi => qi.Id == queueItemId, cancellationToken: _cancellationToken);

        return result;
    }
}
