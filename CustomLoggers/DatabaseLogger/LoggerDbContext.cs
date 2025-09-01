using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MARS.Server.CustomLoggers.DatabaseLogger;

public sealed class LoggerDbContext(DbContextOptions<LoggerDbContext> options)
    : IdentityDbContext(options)
{
    public DbSet<Log> Errors { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("logs");
    }
}
