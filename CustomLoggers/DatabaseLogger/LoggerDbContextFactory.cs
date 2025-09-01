using Microsoft.EntityFrameworkCore.Design;

namespace MARS.Server.CustomLoggers.DatabaseLogger;

public class LoggerDbContextFactory : IDesignTimeDbContextFactory<LoggerDbContext>
{
    public LoggerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LoggerDbContext>();

        // Для миграций используем Development строку подключения
        var connectionString =
            "Host=localhost;Database=mars_dev;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        return new LoggerDbContext(optionsBuilder.Options);
    }
}
