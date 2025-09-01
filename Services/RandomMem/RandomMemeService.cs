using MARS.Server.Services.RandomMem.Entity;

namespace MARS.Server.Services.RandomMem;

public class RandomMemeService(
    IDbContextFactory<AppDbContext> contextFactory,
    ILogger<RandomMemeService> logger
) : IRandomMemeService
{
    #region MemeType CRUD Operations

    public async Task<IEnumerable<MemeType>> GetAllMemeTypesAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RandomMemeType.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<MemeType?> GetMemeTypeByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .RandomMemeType.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<MemeType> CreateMemeTypeAsync(
        MemeType memeType,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new MemeType { Name = memeType.Name, FolderPath = memeType.FolderPath };

        context.RandomMemeType.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created new MemeType: {Name} with ID: {Id}", entity.Name, entity.Id);
        return entity;
    }

    public async Task<MemeType> UpdateMemeTypeAsync(
        MemeType memeType,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var existingEntity = await context.RandomMemeType.FirstOrDefaultAsync(
            x => x.Id == memeType.Id,
            cancellationToken
        );

        if (existingEntity != null)
        {
            existingEntity.Name = memeType.Name;
            existingEntity.FolderPath = memeType.FolderPath;

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Updated MemeType with ID: {Id}", memeType.Id);
            return existingEntity;
        }

        throw new InvalidOperationException($"MemeType with ID {memeType.Id} not found");
    }

    public async Task<bool> DeleteMemeTypeAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await context.RandomMemeType.FirstOrDefaultAsync(
            x => x.Id == id,
            cancellationToken
        );

        if (entity == null)
        {
            return false;
        }

        // Check if there are any MemeOrders using this type
        var hasOrders = await context.RandomMemeOrder.AnyAsync(
            x => x.MemeTypeId == id,
            cancellationToken
        );

        if (hasOrders)
        {
            throw new InvalidOperationException(
                $"Cannot delete MemeType with ID {id} because it has associated MemeOrders"
            );
        }

        context.RandomMemeType.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted MemeType with ID: {Id}", id);
        return true;
    }

    #endregion

    #region MemeOrder CRUD Operations

    public async Task<IEnumerable<MemeOrder>> GetAllMemeOrdersAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .RandomMemeOrder.AsNoTracking()
            .Include(x => x.Type)
            .OrderBy(x => x.MemeTypeId)
            .ThenBy(x => x.Order)
            .ToListAsync(cancellationToken);
    }

    public async Task<MemeOrder?> GetMemeOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .RandomMemeOrder.AsNoTracking()
            .Include(x => x.Type)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<MemeOrder>> GetMemeOrdersByTypeAsync(
        int typeId,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context
            .RandomMemeOrder.AsNoTracking()
            .Include(x => x.Type)
            .Where(x => x.MemeTypeId == typeId)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);
    }

    public async Task<MemeOrder> CreateMemeOrderAsync(
        MemeOrder memeOrder,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Get the next order number for this type
        var maxOrder =
            await context
                .RandomMemeOrder.Where(x => x.MemeTypeId == memeOrder.MemeTypeId)
                .MaxAsync(x => (int?)x.Order, cancellationToken) ?? 0;

        var entity = new MemeOrder
        {
            FilePath = memeOrder.FilePath,
            MemeTypeId = memeOrder.MemeTypeId,
            Order = maxOrder + 1,
        };

        context.RandomMemeOrder.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created new MemeOrder: {FilePath} with Order: {Order}",
            entity.FilePath,
            entity.Order
        );
        return entity;
    }

    public async Task<MemeOrder> UpdateMemeOrderAsync(
        MemeOrder memeOrder,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var existingEntity = await context.RandomMemeOrder.FirstOrDefaultAsync(
            x => x.Id == memeOrder.Id,
            cancellationToken
        );

        if (existingEntity != null)
        {
            existingEntity.FilePath = memeOrder.FilePath;
            existingEntity.MemeTypeId = memeOrder.MemeTypeId;
            existingEntity.Order = memeOrder.Order;

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Updated MemeOrder with ID: {Id}", memeOrder.Id);
            return existingEntity;
        }

        throw new InvalidOperationException($"MemeOrder with ID {memeOrder.Id} not found");
    }

    public async Task<bool> DeleteMemeOrderAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await context.RandomMemeOrder.FirstOrDefaultAsync(
            x => x.Id == id,
            cancellationToken
        );

        if (entity == null)
        {
            return false;
        }

        context.RandomMemeOrder.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        // Reorder remaining orders for this type
        await ReorderMemeOrdersAsync(entity.MemeTypeId ?? 0, cancellationToken);

        logger.LogInformation("Deleted MemeOrder with ID: {Id}", id);
        return true;
    }

    #endregion

    #region Additional Operations

    public async Task<MemeOrder?> GetRandomMemeAsync(
        int? typeId = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.RandomMemeOrder.AsNoTracking();

        if (typeId.HasValue)
        {
            query = query.Where(x => x.MemeTypeId == typeId.Value);
        }

        var count = await query.CountAsync(cancellationToken);
        if (count == 0)
        {
            return null;
        }

        var randomIndex = Random.Shared.Next(count);
        return await query.Include(x => x.Type).Skip(randomIndex).FirstAsync(cancellationToken);
    }

    public async Task<int> GetMemeOrderCountAsync(
        int? typeId = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.RandomMemeOrder.AsNoTracking();

        if (typeId.HasValue)
        {
            query = query.Where(x => x.MemeTypeId == typeId.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task ReorderMemeOrdersAsync(
        int typeId,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var orders = await context
            .RandomMemeOrder.Where(x => x.MemeTypeId == typeId)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < orders.Count; i++)
        {
            orders[i].Order = i + 1;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Reordered {Count} MemeOrders for type {TypeId}",
            orders.Count,
            typeId
        );
    }

    #endregion
}
