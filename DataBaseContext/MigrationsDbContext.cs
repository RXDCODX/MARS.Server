using Microsoft.EntityFrameworkCore;

namespace MARS.Server.DataBaseContext;

/// <summary>
/// Контекст для EF Core design-time миграций (dotnet ef migrations add).
/// Наследует AppDbContext без логики авто-миграции.
/// </summary>
public sealed class MigrationsDbContext : AppDbContext
{
    public MigrationsDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }
}
