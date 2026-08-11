using MARS.Server.DataBaseContext;
using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.CinemaQueue.Repositories;

public class CinemaQueueRepository(IDbContextFactory<AppDbContext> dbContextFactory)
    : ICinemaQueueRepository
{
    public async Task<IEnumerable<CinemaMediaItem>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .CinemaQueue.AsNoTracking()
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CinemaMediaItem?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .CinemaQueue.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<CinemaMediaItem?> GetNextAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .CinemaQueue.AsNoTracking()
            .Where(x => x.Status == MediaStatus.Pending && x.IsNext)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<CinemaMediaItem>> GetByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .CinemaQueue.AsNoTracking()
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CinemaMediaItem> CreateAsync(
        CinemaMediaItem cinemaMediaItem,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CinemaQueue.AddAsync(cinemaMediaItem, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return entity.Entity;
    }

    public async Task<CinemaMediaItem?> UpdateAsync(
        CinemaMediaItem cinemaMediaItem,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingItem = await context.CinemaQueue.FindAsync(
            [cinemaMediaItem.Id],
            cancellationToken
        );

        if (existingItem == null)
        {
            return null;
        }

        cinemaMediaItem.LastModified = DateTime.Now;
        context.Entry(existingItem).CurrentValues.SetValues(cinemaMediaItem);
        await context.SaveChangesAsync(cancellationToken);

        return existingItem;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.CinemaQueue.FindAsync([id], cancellationToken);

        if (item == null)
        {
            return false;
        }

        context.CinemaQueue.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ResetNextFlagsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await context.CinemaQueue.Where(x => x.IsNext).ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.IsNext = false;
            item.LastModified = DateTime.Now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetCountByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .CinemaQueue.AsNoTracking()
            .CountAsync(x => x.Status == status, cancellationToken);
    }
}
