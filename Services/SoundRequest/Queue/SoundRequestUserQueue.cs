using MARS.Server.Services.SoundRequest.Entities;

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
    public async Task<BaseTrackInfo> AddToQueueAsync(BaseTrackInfo track)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        // Получаем максимальный порядок в очереди
        var maxOrder =
            await dbContext
                .SoundRequestBaseTrackInfos.AsNoTracking()
                .Where(t => t.QueueOrder != null)
                .MaxAsync(t => (int?)t.QueueOrder, cancellationToken: _cancellationToken) ?? -1;

        track.QueueOrder = maxOrder + 1;
        track.IsDeleted = false;

        dbContext.SoundRequestBaseTrackInfos.Add(track);
        await dbContext.SaveChangesAsync(_cancellationToken);

        return track;
    }

    /// <summary>
    /// Удалить трек из очереди (помечает как удаленный)
    /// </summary>
    public async Task RemoveFromQueueAsync(Guid id)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        var trackToRemove = await dbContext.SoundRequestBaseTrackInfos.FindAsync(
            [id],
            cancellationToken: _cancellationToken
        );

        if (trackToRemove == null)
        {
            return;
        }

        var removedOrder = trackToRemove.QueueOrder;

        // Помечаем как удаленный
        trackToRemove.IsDeleted = true;
        trackToRemove.QueueOrder = null;
        dbContext.SoundRequestBaseTrackInfos.Update(trackToRemove);

        // Обновляем порядок остальных треков в очереди
        if (removedOrder.HasValue)
        {
            await dbContext
                .SoundRequestBaseTrackInfos.Where(t =>
                    t.QueueOrder > removedOrder.Value && !t.IsDeleted
                )
                .ExecuteUpdateAsync(
                    e => e.SetProperty(t => t.QueueOrder, t => t.QueueOrder - 1),
                    cancellationToken: _cancellationToken
                );
        }

        await dbContext.SaveChangesAsync(_cancellationToken);
    }

    /// <summary>
    /// Получить очередь треков (только не удаленные)
    /// </summary>
    public async Task<List<BaseTrackInfo>> GetQueueAsync()
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        return await dbContext
            .SoundRequestBaseTrackInfos.AsNoTracking()
            .Include(e => e.RequestedByTwitchUser)
            .Where(t => !t.IsDeleted && t.QueueOrder != null)
            .OrderBy(t => t.QueueOrder)
            .ToListAsync(cancellationToken: _cancellationToken);
    }

    /// <summary>
    /// Получить следующий трек из очереди
    /// </summary>
    public async Task<BaseTrackInfo?> GetNextTrackAsync()
    {
        BaseTrackInfo? result = null;

        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        result = await dbContext
            .SoundRequestBaseTrackInfos.AsNoTracking()
            .Where(t => !t.IsDeleted && t.QueueOrder != null)
            .OrderBy(t => t.QueueOrder)
            .FirstOrDefaultAsync(cancellationToken: _cancellationToken);

        return result;
    }

    /// <summary>
    /// Получить количество треков в очереди
    /// </summary>
    public async Task<int> GetQueueCountAsync()
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        var result = await dbContext.SoundRequestBaseTrackInfos.CountAsync(
            t => !t.IsDeleted && t.QueueOrder != null,
            cancellationToken: _cancellationToken
        );

        return result;
    }

    /// <summary>
    /// Получить трек по ID
    /// </summary>
    public async Task<BaseTrackInfo?> GetTrackByIdAsync(Guid id)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        BaseTrackInfo? result = await dbContext
            .SoundRequestBaseTrackInfos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken: _cancellationToken);

        return result;
    }

    /// <summary>
    /// Получить треки пользователя из очереди
    /// </summary>
    public async Task<List<BaseTrackInfo>> GetUserTracksAsync(string twitchId)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);

        List<BaseTrackInfo> result = await dbContext
            .SoundRequestBaseTrackInfos.AsNoTracking()
            .Where(t => !t.IsDeleted && t.QueueOrder != null && t.RequestedByTwitchId == twitchId)
            .OrderBy(t => t.QueueOrder)
            .ToListAsync(cancellationToken: _cancellationToken);

        return result;
    }
}
