namespace MARS.Server.CustomLoggers.DatabaseLogger;

public class DbLoggerOptions
{
    private LoggerDbContext? _dbContext;
    public required LoggerDbContext DbContext
    {
        get { return _dbContext ?? throw new NullReferenceException(); }
        set { _dbContext = value ?? throw new NullReferenceException(); }
    }
}
