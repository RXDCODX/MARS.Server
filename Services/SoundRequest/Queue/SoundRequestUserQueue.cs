using MARS.Server.Services.SoundRequest.Entities;

namespace MARS.Server.Services.SoundRequest.Queue;

public class SoundRequestUserQueue(
    IDbContextFactory<AppDbContext> contextFactory,
    IHostApplicationLifetime lifetime
)
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    public async Task<UserRequestedTrack> AddToQueueAsync(UserRequestedTrack track)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);
        var maxOrder = await dbContext
            .SoundRequestUserQueue.AsNoTracking()
            .MaxAsync(t => (int?)t.Order, cancellationToken: _cancellationToken) ?? 0;

        track.Order = maxOrder + 1;
        dbContext.SoundRequestUserQueue.Add(track);
        await dbContext.SaveChangesAsync(_cancellationToken);

        return track;
    }

    public async Task RemoveFromQueueAsync(Guid id)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);
        var trackToRemove = await dbContext.SoundRequestUserQueue.FindAsync(id);
        if (trackToRemove == null)
        {
            return;
        }

        var removedOrder = trackToRemove.Order;
        dbContext.SoundRequestUserQueue.Remove(trackToRemove);

        await dbContext
            .SoundRequestUserQueue.Where(t => t.Order > removedOrder)
            .ExecuteUpdateAsync(
                e => e.SetProperty(t => t.Order, t => t.Order - 1),
                cancellationToken: _cancellationToken
            );

        await dbContext.SaveChangesAsync(_cancellationToken);
    }

    public async Task<List<UserRequestedTrack>> GetQueueAsync()
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(_cancellationToken);
        return await dbContext
            .SoundRequestUserQueue.AsNoTracking()
            .OrderBy(t => t.Order)
            .ToListAsync(cancellationToken: _cancellationToken);
    }
}


