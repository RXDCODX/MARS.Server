namespace MARS.Server.DataBaseContext;

public class AppDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly DbContextOptions<AppDbContext> _options;

    public AppDbContextFactory(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder<AppDbContext>> action
    )
    {
        _environment = environment;
        _configuration = configuration;

        var options = new DbContextOptionsBuilder<AppDbContext>();
        action.Invoke(options);
        _options = options.Options;
    }

    public AppDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder
            .UseNpgsql(_configuration.GetConnectionString("DATABASE_Path"))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

        if (_environment.IsDevelopment())
        {
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.EnableThreadSafetyChecks();
            optionsBuilder.EnableSensitiveDataLogging();
        }

        return new AppDbContext(_options);
    }
}
