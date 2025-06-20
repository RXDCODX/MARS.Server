using Microsoft.EntityFrameworkCore.Design;

namespace MARS.Server.DataBaseContext;

public class AppDbContextFactory
    : IDbContextFactory<AppDbContext>,
        IDesignTimeDbContextFactory<AppDbContext>
{
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
        return GetDbContext(isMigrations: false);
    }

    // Реализация IDesignTimeDbContextFactory<AppDbContext>
    public AppDbContext CreateDbContext(string[] args)
    {
        return GetDbContext(isMigrations: true);
    }

    private AppDbContext GetDbContext(bool isMigrations)
    {
        if (_options != null && !isMigrations)
        {
            // Используем предварительно настроенные опции (если фабрика создана через DI)
            return new(_options, isMigrations);
        }

        // Настройка вручную (для миграций)
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

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

        return new(optionsBuilder.Options, isMigrations);
    }
}
