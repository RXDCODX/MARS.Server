using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
        if (_options != null)
        {
            return new AppDbContext(_options, isMigrations);
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

        return new AppDbContext(optionsBuilder.Options, isMigrations);
    }
}
