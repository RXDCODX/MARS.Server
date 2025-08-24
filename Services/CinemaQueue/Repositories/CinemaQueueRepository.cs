using MARS.Server.DataBaseContext;
using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using Microsoft.EntityFrameworkCore;
using MediaType = MARS.Server.Services.CinemaQueue.Entitys.MediaType;

namespace MARS.Server.Services.CinemaQueue.Repositories;

public class CinemaQueueRepository : ICinemaQueueRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public CinemaQueueRepository(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IEnumerable<MediaItem>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .Set<MediaItem>()
            .AsNoTracking()
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<MediaItem?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .Set<MediaItem>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<MediaItem?> GetNextAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .Set<MediaItem>()
            .AsNoTracking()
            .Where(x => x.Status == MediaStatus.Pending && x.IsNext)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<MediaItem>> GetByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .Set<MediaItem>()
            .AsNoTracking()
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<MediaItem>> GetByTypeAsync(
        MediaType type,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .Set<MediaItem>()
            .AsNoTracking()
            .Where(x => x.Type == type)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<MediaItem> CreateAsync(
        MediaItem mediaItem,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<MediaItem>().AddAsync(mediaItem, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return entity.Entity;
    }

    public async Task<MediaItem?> UpdateAsync(
        MediaItem mediaItem,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingItem = await context
            .Set<MediaItem>()
            .FindAsync([mediaItem.Id], cancellationToken);

        if (existingItem == null)
        {
            return null;
        }

        mediaItem.LastModified = DateTimeOffset.Now;
        context.Entry(existingItem).CurrentValues.SetValues(mediaItem);
        await context.SaveChangesAsync(cancellationToken);

        return existingItem;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.Set<MediaItem>().FindAsync([id], cancellationToken);

        if (item == null)
        {
            return false;
        }

        context.Set<MediaItem>().Remove(item);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ResetNextFlagsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await context
            .Set<MediaItem>()
            .Where(x => x.IsNext)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.IsNext = false;
            item.LastModified = DateTimeOffset.Now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetCountByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .Set<MediaItem>()
            .AsNoTracking()
            .CountAsync(x => x.Status == status, cancellationToken);
    }

    public async Task<int> GetCountByTypeAsync(
        MediaType type,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .Set<MediaItem>()
            .AsNoTracking()
            .CountAsync(x => x.Type == type, cancellationToken);
    }
}
