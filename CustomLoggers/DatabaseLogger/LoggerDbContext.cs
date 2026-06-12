using System.Linq;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MARS.Server.CustomLoggers.DatabaseLogger;

public sealed class LoggerDbContext : DbContext
{
    private static readonly Lock Locker = new();
    private static bool _isMigrated;

    public LoggerDbContext(DbContextOptions<LoggerDbContext> options, bool isMigrations = false)
        : base(options)
    {
        if (!_isMigrated && !isMigrations)
        {
            Locker.Enter();
            if (!_isMigrated)
            {
                var migrations = Database.GetPendingMigrations();

                if (migrations.Any())
                {
                    Database.Migrate();
                }

                _isMigrated = true;
            }

            Locker.Exit();
        }
    }

    public DbSet<Log> Logs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("logs");

        builder.Entity<Log>().Property(e => e.LogLevel).HasConversion<string>();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConversion>();

        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeToDateTimeUtc>();
    }

    public sealed class DateTimeOffsetConversion()
        : ValueConverter<DateTimeOffset, DateTimeOffset>(
            offset => offset.Offset != TimeSpan.Zero ? offset.ToOffset(TimeSpan.Zero) : offset,
            v => v.ToLocalTime()
        );

    public sealed class DateTimeToDateTimeUtc()
        : ValueConverter<DateTime, DateTime>(
            c => DateTime.SpecifyKind(c, DateTimeKind.Utc),
            c => c
        );
}
