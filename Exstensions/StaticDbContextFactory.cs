using MARS.Server.DataBaseContext;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Exstensions;

public static class StaticDbContextFactory
{
    public static IDbContextFactory<AppDbContext>? Factory { get; set; }
}
