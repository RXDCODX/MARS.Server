using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MARS.Server.DataBaseContext;

public class AppDbContextFactory
    : IDbContextFactory<AppDbContext>,
        IDesignTimeDbContextFactory<MigrationsDbContext>
{
    private static readonly Lock Locker = new();
    private static bool _isMigrated;

    private readonly DbContextOptions<AppDbContext>? _options;

    // Конструктор для обычного использования (с DI)
    public AppDbContextFactory(Action<DbContextOptionsBuilder<AppDbContext>> optionsAction)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsAction.Invoke(optionsBuilder);
        _options = optionsBuilder.Options;
    }

    // Конструктор для миграций (без DI)
    public AppDbContextFactory()
    {
        // В режиме миграций настройки создаются вручную
    }

    // Реализация IDbContextFactory<AppDbContext>
    public AppDbContext CreateDbContext()
    {
        var context = GetDbContext();
        MigrateIfNeeded();
        return context;
    }

    // Реализация IDesignTimeDbContextFactory<MigrationsDbContext>
    public MigrationsDbContext CreateDbContext(string[] args)
    {
        // Настройка вручную (для design-time миграций)
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("Dev_Path");

        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        optionsBuilder.EnableThreadSafetyChecks();

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (environment == Environments.Development)
        {
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.EnableSensitiveDataLogging();
        }

        return new MigrationsDbContext(optionsBuilder.Options);
    }

    private AppDbContext GetDbContext()
    {
        if (_options != null)
        {
            return new AppDbContext(_options);
        }

        // Настройка вручную (для design-time миграций)
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("Dev_Path");

        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        optionsBuilder.EnableThreadSafetyChecks();

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (environment == Environments.Development)
        {
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.EnableSensitiveDataLogging();
        }

        return new AppDbContext(optionsBuilder.Options);
    }

    private void MigrateIfNeeded()
    {
        if (_isMigrated)
        {
            return;
        }

        Locker.Enter();
        try
        {
            var context = CreateDbContext([]);

            if (!_isMigrated)
            {
                try
                {
                    context.Database.Migrate();
                }
                catch (Npgsql.PostgresException)
                {
                    // Миграция не полностью применилась (БД восстановлена из бэкапа со старой схемой)
                    // Сервер продолжит работу, недостающие таблицы вызовут ошибки при запросах
                }

                _isMigrated = true;
            }
        }
        finally
        {
            Locker.Exit();
        }
    }
}
