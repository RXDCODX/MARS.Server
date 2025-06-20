namespace MARS.Server.CustomLoggers.DatabaseLogger;

public class DbLoggerOptions
{
    private LoggerDbContext? _dbContext;
    public required LoggerDbContext DbContext
    {
        get
        {
            if (_dbContext is null)
            {
                throw new NullReferenceException();
            }

            return _dbContext;
        }
        set { _dbContext = value ?? throw new NullReferenceException(); }
    }
}
