using Microsoft.EntityFrameworkCore.Design;

namespace MARS.Server.CustomLoggers.DatabaseLogger;

public class LoggerDbContextFactory
    : IDbContextFactory<LoggerDbContext>,
        IDesignTimeDbContextFactory<LoggerDbContext>
{
    private readonly DbContextOptions<LoggerDbContext>? _options;

    public LoggerDbContextFactory(Action<DbContextOptionsBuilder<LoggerDbContext>> optionsAction)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LoggerDbContext>();
        optionsAction.Invoke(optionsBuilder);
        _options = optionsBuilder.Options;
    }

    // Конструктор для миграций (без DI)
    public LoggerDbContextFactory()
    {
        // В режиме миграций настройки создаются вручную
    }

    public LoggerDbContext CreateDbContext()
    {
        return GetDbContext(false);
    }

    public LoggerDbContext CreateDbContext(string[] args)
    {
        return GetDbContext(true);
    }

    private LoggerDbContext GetDbContext(bool isMigrations)
    {
        if (_options != null && !isMigrations)
        {
            // Используем предварительно настроенные опции (если фабрика создана через DI)
            return new LoggerDbContext(_options, isMigrations);
        }

        // Настройка вручную (для миграций)
        var optionsBuilder = new DbContextOptionsBuilder<LoggerDbContext>();

        // Загружаем конфигурацию из appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.Development.json", optional: true)
            .Build();

        // Получаем строку подключения
        var connectionString = configuration.GetConnectionString("Dev_Path"); // Или "Prod_Path", если нужно

        // Настраиваем DbContext
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        optionsBuilder.EnableThreadSafetyChecks();

        // Включаем детализированные ошибки в Development
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (environment == Environments.Development)
        {
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.EnableSensitiveDataLogging();
        }

        return new LoggerDbContext(optionsBuilder.Options, true);
    }
}
