namespace MARS.Server.Exstensions;

public static class StaticDbContextFactory
{
    public static IDbContextFactory<AppDbContext>? Factory { get; set; }
}
