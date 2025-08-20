using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Repositories;

/// <summary>
/// Репозиторий для работы с пользователями Honkai в базе данных
/// </summary>
public class HonkaiUserRepository : IHonkaiUserRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<HonkaiUserRepository> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр репозитория пользователей Honkai
    /// </summary>
    /// <param name="dbContextFactory">Фабрика контекста базы данных</param>
    /// <param name="logger">Логгер для записи событий</param>
    public HonkaiUserRepository(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<HonkaiUserRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Получает всех пользователей, которым нужна ежедневная отметка
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Список пользователей, которым нужна отметка</returns>
    public async Task<List<DailyAutoMarkupUser>> GetUsersNeedingDailyMarkupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            var users = await dbContext
                .HonkaiMarkupUser
                .AsNoTracking()
                .Where(u => u.LastAutoMarkup < DateTime.UtcNow.Date)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Found {Count} users needing daily markup", users.Count);
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users needing daily markup");
            return new List<DailyAutoMarkupUser>();
        }
    }

    /// <summary>
    /// Получает всех пользователей для проверки энергии
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Список всех пользователей</returns>
    public async Task<List<DailyAutoMarkupUser>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            var users = await dbContext.HonkaiMarkupUser.AsNoTracking().ToListAsync(cancellationToken);
            
            _logger.LogDebug("Retrieved {Count} total users", users.Count);
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all users");
            return new List<DailyAutoMarkupUser>();
        }
    }

    /// <summary>
    /// Получает пользователей, у которых есть ошибки с отметками
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Список пользователей с ошибками</returns>
    public async Task<List<DailyAutoMarkupUser>> GetUsersWithMarkupErrorsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            var users = await dbContext
                .HonkaiMarkupUser
                .AsNoTracking()
                .Where(u => u.LastAutoMarkup < DateTime.UtcNow.Date)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Found {Count} users with markup errors", users.Count);
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users with markup errors");
            return new List<DailyAutoMarkupUser>();
        }
    }

    /// <summary>
    /// Обновляет время последней отметки пользователя
    /// </summary>
    /// <param name="user">Пользователь для обновления</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если обновление прошло успешно</returns>
    public async Task<bool> UpdateLastMarkupTimeAsync(DailyAutoMarkupUser user, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            user.LastAutoMarkup = DateTime.UtcNow;
            dbContext.HonkaiMarkupUser.Update(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Updated last markup time for user {UserId}", user.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last markup time for user {UserId}", user.Id);
            return false;
        }
    }

    /// <summary>
    /// Создает нового пользователя
    /// </summary>
    /// <param name="user">Данные пользователя</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Созданный пользователь</returns>
    public async Task<DailyAutoMarkupUser> CreateUserAsync(DailyAutoMarkupUser user, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            await dbContext.HonkaiMarkupUser.AddAsync(user, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created new Honkai user with ID: {UserId}", user.Id);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new user");
            throw;
        }
    }

    /// <summary>
    /// Проверяет существование пользователей в базе данных
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если пользователи существуют</returns>
    public async Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            var exists = await dbContext.HonkaiMarkupUser.AsNoTracking().AnyAsync(cancellationToken);
            
            _logger.LogDebug("Users exist check result: {Exists}", exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if users exist");
            return false;
        }
    }
}