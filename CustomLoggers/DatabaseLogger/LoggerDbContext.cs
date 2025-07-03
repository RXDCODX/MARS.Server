using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MARS.Server.CustomLoggers.DatabaseLogger;

public sealed class LoggerDbContext : IdentityDbContext
{
    private bool _isMigrated;
    private readonly bool _isMigrations;
    private readonly Lock _locker = new();
    public DbSet<Log> Errors { get; set; } = null!;

    public LoggerDbContext(DbContextOptions<LoggerDbContext> options)
        : base(options) { }

    public LoggerDbContext()
    {
        _isMigrations = true;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_isMigrations)
        {
            // Загружаем конфигурацию из appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.Development.json", optional: true)
                .Build();

            // Включаем детализированные ошибки в Development
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            string connectionString;

            switch (environment)
            {
                case "Development":
                    connectionString = configuration.GetConnectionString("Dev_Path")!;
                    break;
                case "Production":
                    connectionString = configuration.GetConnectionString("Prod_Path")!;
                    break;
                default:
                    return;
            }

            optionsBuilder.UseNpgsql(connectionString);
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            optionsBuilder.EnableThreadSafetyChecks();
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.EnableSensitiveDataLogging();

            if (!_isMigrated)
            {
                _locker.Enter();
                if (!_isMigrated)
                {
                    var migrations = Database.GetPendingMigrations();

                    if (migrations.Any())
                    {
                        Database.Migrate();
                    }

                    _isMigrated = true;
                }

                _locker.Exit();
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("logs");
    }
}
