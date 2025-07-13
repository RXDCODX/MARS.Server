using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MARS.Server.Exstensions;

public static class DbSetExstension
{
    public static EntityEntry<T> AddOrUpdate<T>(this DbSet<T> dbContext, T entity)
        where T : class
    {
        var entityEntry = dbContext.Entry(entity);
        var state = entityEntry.State;
        return state switch
        {
            EntityState.Detached => dbContext.Add(entity),
            EntityState.Modified => dbContext.Update(entity),
            EntityState.Unchanged => dbContext.Update(entity),
            _ => entityEntry,
        };
    }
}
