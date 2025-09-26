using MARS.Server.Services.SoundRequest.Entitys;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Manages the queue of users for sound requests.
/// </summary>
public class SoundRequestUserQueue(
    IDbContextFactory<AppDbContext> contextFactory,
    IHostApplicationLifetime lifetime
)
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    // Добавление нового запроса в очередь
    public async Task<UserRequestedTrack> AddToQueueAsync(UserRequestedTrack track)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);
        // Получаем максимальный текущий Order
        var maxOrder = await dbContext.SoundRequestUserQueue.MaxAsync(
            t => t.Order,
            cancellationToken: _cancellationToken
        );

        track.Order = maxOrder + 1;
        dbContext.SoundRequestUserQueue.Add(track);
        await dbContext.SaveChangesAsync(_cancellationToken);

        return track;
    }

    // Удаление запроса из очереди с пересчетом Order
    public async Task RemoveFromQueueAsync(Guid trackId)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);
        var trackToRemove = await dbContext.SoundRequestUserQueue.FindAsync(trackId);
        if (trackToRemove == null)
        {
            return;
        }

        var removedOrder = trackToRemove.Order;

        // Удаляем трек
        dbContext.SoundRequestUserQueue.Remove(trackToRemove);

        // Обновляем Order для всех треков, которые были после удаленного
        await dbContext
            .SoundRequestUserQueue.Where(t => t.Order > removedOrder)
            .ExecuteUpdateAsync(
                e => e.SetProperty(t => t.Order, t => t.Order - 1),
                cancellationToken: _cancellationToken
            );

        await dbContext.SaveChangesAsync(_cancellationToken);
    }

    // Получение текущей очереди в правильном порядке
    public async Task<List<UserRequestedTrack>> GetQueueAsync()
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);
        return await dbContext
            .SoundRequestUserQueue.OrderBy(t => t.Order)
            .ToListAsync(cancellationToken: _cancellationToken);
    }
}
