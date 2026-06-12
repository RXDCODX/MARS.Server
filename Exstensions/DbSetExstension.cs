using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MARS.Server.Exstensions;

public static class DbSetExstension
{
    extension<T>(DbSet<T> dbContext) where T : class
    {
        public EntityEntry<T> AddOrUpdate(T entity)
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
}
