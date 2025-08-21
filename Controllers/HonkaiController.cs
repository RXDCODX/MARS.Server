using MARS.Server.Services.Honkai.Entitys;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HonkaiController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<HonkaiController> _logger;

    public HonkaiController(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<HonkaiController> logger
    )
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Получить всех пользователей автоматических отметок
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<DailyAutoMarkupUser>>> GetUsers()
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var users = await dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .OrderBy(u => u.CreatedAt)
                .ToListAsync();

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении пользователей автоматических отметок");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить пользователя по ID
    /// </summary>
    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<DailyAutoMarkupUser>> GetUser(Guid id)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var user = await dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound($"Пользователь с ID {id} не найден");
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении пользователя {UserId}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Создать нового пользователя автоматических отметок
    /// </summary>
    [HttpPost("users")]
    public async Task<ActionResult<DailyAutoMarkupUser>> CreateUser(
        [FromBody] CreateUserRequest request
    )
    {
        try
        {
            if (
                string.IsNullOrEmpty(request.LtmidV2)
                || string.IsNullOrEmpty(request.LTokenV2)
                || string.IsNullOrEmpty(request.LtuidV2)
            )
            {
                return BadRequest("Все обязательные поля должны быть заполнены");
            }

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Проверяем, не существует ли уже пользователь с такими данными
            var existingUser = await dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.LtmidV2 == request.LtmidV2
                    && u.LTokenV2 == request.LTokenV2
                    && u.LtuidV2 == request.LtuidV2
                );

            if (existingUser != null)
            {
                return Conflict("Пользователь с такими учетными данными уже существует");
            }

            var user = new DailyAutoMarkupUser
            {
                TwitchId = request.TwitchId,
                TelegramId = request.TelegramId,
                LtmidV2 = request.LtmidV2,
                LTokenV2 = request.LTokenV2,
                LtuidV2 = request.LtuidV2,
                CreatedAt = DateTime.UtcNow,
                LastAutoMarkup = DateTime.UtcNow.AddDays(-1), // Позволяет получить отметки сразу
            };

            dbContext.HonkaiMarkupUser.Add(user);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Создан новый пользователь автоматических отметок: {UserId}",
                user.Id
            );

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании пользователя автоматических отметок");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Обновить пользователя
    /// </summary>
    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<DailyAutoMarkupUser>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request
    )
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var user = await dbContext.HonkaiMarkupUser.FindAsync(id);

            if (user == null)
            {
                return NotFound($"Пользователь с ID {id} не найден");
            }

            // Обновляем только разрешенные поля
            if (request.TwitchId != null)
            {
                user.TwitchId = request.TwitchId;
            }

            if (request.TelegramId != null)
            {
                user.TelegramId = request.TelegramId;
            }

            if (!string.IsNullOrEmpty(request.LtmidV2))
            {
                user.LtmidV2 = request.LtmidV2;
            }

            if (!string.IsNullOrEmpty(request.LTokenV2))
            {
                user.LTokenV2 = request.LTokenV2;
            }

            if (!string.IsNullOrEmpty(request.LtuidV2))
            {
                user.LtuidV2 = request.LtuidV2;
            }

            await dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Обновлен пользователь автоматических отметок: {UserId}",
                user.Id
            );

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении пользователя {UserId}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Удалить пользователя
    /// </summary>
    [HttpDelete("users/{id:guid}")]
    public async Task<ActionResult> DeleteUser(Guid id)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var user = await dbContext.HonkaiMarkupUser.FindAsync(id);

            if (user == null)
            {
                return NotFound($"Пользователь с ID {id} не найден");
            }

            dbContext.HonkaiMarkupUser.Remove(user);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Удален пользователь автоматических отметок: {UserId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении пользователя {UserId}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Принудительно активировать ежедневные отметки для пользователя
    /// </summary>
    [HttpPost("users/{id:guid}/redeem-now")]
    public async Task<ActionResult> RedeemNow(Guid id)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var user = await dbContext.HonkaiMarkupUser.FindAsync(id);

            if (user == null)
            {
                return NotFound($"Пользователь с ID {id} не найден");
            }

            // Сбрасываем время последней отметки, чтобы сервис мог снова активировать отметки
            user.LastAutoMarkup = DateTime.UtcNow.AddDays(-1);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Сброшено время последней отметки для пользователя {UserId}",
                id
            );

            return Ok(
                new
                {
                    message = "Время последней отметки сброшено. Отметки будут активированы при следующей проверке.",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сбросе времени отметки для пользователя {UserId}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить статистику пользователей
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStats()
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var totalUsers = await dbContext.HonkaiMarkupUser.CountAsync();
            var usersWithTelegram = await dbContext.HonkaiMarkupUser.CountAsync(u =>
                u.TelegramId.HasValue
            );
            var usersWithTwitch = await dbContext.HonkaiMarkupUser.CountAsync(u =>
                !string.IsNullOrEmpty(u.TwitchId)
            );
            var usersWithBoth = await dbContext.HonkaiMarkupUser.CountAsync(u =>
                u.TelegramId.HasValue && !string.IsNullOrEmpty(u.TwitchId)
            );

            var today = DateTime.UtcNow.Date;
            var usersMarkedToday = await dbContext.HonkaiMarkupUser.CountAsync(u =>
                u.LastAutoMarkup >= today
            );

            var stats = new
            {
                TotalUsers = totalUsers,
                UsersWithTelegram = usersWithTelegram,
                UsersWithTwitch = usersWithTwitch,
                UsersWithBoth = usersWithBoth,
                UsersMarkedToday = usersMarkedToday,
                UsersNotMarkedToday = totalUsers - usersMarkedToday,
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статистики");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}

public class CreateUserRequest
{
    public string? TwitchId { get; set; }
    public long? TelegramId { get; set; }
    public required string LtmidV2 { get; set; }
    public required string LTokenV2 { get; set; }
    public required string LtuidV2 { get; set; }
}

public class UpdateUserRequest
{
    public string? TwitchId { get; set; }
    public long? TelegramId { get; set; }
    public string? LtmidV2 { get; set; }
    public string? LTokenV2 { get; set; }
    public string? LtuidV2 { get; set; }
}
