using MARS.Server.Services;
using MARS.Server.Services.Honkai.Entitys;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HonkaiController(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<HonkaiController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить всех пользователей автоматических отметок
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<OperationResult<List<DailyAutoMarkupUser>>>> GetUsers()
    {
        ActionResult<OperationResult<List<DailyAutoMarkupUser>>> result = null!;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var users = await dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .OrderBy(u => u.CreatedAt)
                .ToListAsync();

            result = Ok(
                OperationResult<List<DailyAutoMarkupUser>>.Ok(
                    "Получены пользователи автоматических отметок",
                    users
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении пользователей автоматических отметок");
            result = Ok(
                OperationResult<List<DailyAutoMarkupUser>>.Bad(
                    "Ошибка при получении пользователей",
                    []
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить пользователя по ID
    /// </summary>
    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<OperationResult<DailyAutoMarkupUser?>>> GetUser(Guid id)
    {
        ActionResult<OperationResult<DailyAutoMarkupUser?>> result = null!;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var user = await dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user != null)
            {
                result = Ok(OperationResult<DailyAutoMarkupUser?>.Ok("Пользователь найден", user));
            }
            else
            {
                result = Ok(
                    OperationResult<DailyAutoMarkupUser?>.Bad(
                        $"Пользователь с ID {id} не найден",
                        null
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении пользователя {UserId}", id);
            result = Ok(
                OperationResult<DailyAutoMarkupUser?>.Bad("Ошибка при получении пользователя", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Создать нового пользователя автоматических отметок
    /// </summary>
    [HttpPost("users")]
    public async Task<ActionResult<OperationResult<DailyAutoMarkupUser?>>> CreateUser(
        [FromBody] CreateUserRequest request
    )
    {
        ActionResult<OperationResult<DailyAutoMarkupUser?>> result = null!;

        try
        {
            if (
                !string.IsNullOrEmpty(request.LtmidV2)
                && !string.IsNullOrEmpty(request.LTokenV2)
                && !string.IsNullOrEmpty(request.LtuidV2)
            )
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();

                // Проверяем, не существует ли уже пользователь с такими данными
                var existingUser = await dbContext
                    .HonkaiMarkupUser.AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        u.LtmidV2 == request.LtmidV2
                        && u.LTokenV2 == request.LTokenV2
                        && u.LtuidV2 == request.LtuidV2
                    );

                if (existingUser == null)
                {
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

                    logger.LogInformation(
                        "Создан новый пользователь автоматических отметок: {UserId}",
                        user.Id
                    );

                    result = Ok(
                        OperationResult<DailyAutoMarkupUser?>.Ok(
                            "Пользователь успешно создан",
                            user
                        )
                    );
                }
                else
                {
                    result = Ok(
                        OperationResult<DailyAutoMarkupUser?>.Bad(
                            "Пользователь с такими учетными данными уже существует",
                            null
                        )
                    );
                }
            }
            else
            {
                result = Ok(
                    OperationResult<DailyAutoMarkupUser?>.Bad(
                        "Все обязательные поля должны быть заполнены",
                        null
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании пользователя автоматических отметок");
            result = Ok(
                OperationResult<DailyAutoMarkupUser?>.Bad("Ошибка при создании пользователя", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Обновить пользователя
    /// </summary>
    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<OperationResult<DailyAutoMarkupUser?>>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request
    )
    {
        ActionResult<OperationResult<DailyAutoMarkupUser?>> result = null!;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var user = await dbContext.HonkaiMarkupUser.FindAsync(id);

            if (user == null)
            {
                result = Ok(
                    OperationResult<DailyAutoMarkupUser?>.Bad(
                        $"Пользователь с ID {id} не найден",
                        null
                    )
                );
            }
            else
            {
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

                logger.LogInformation(
                    "Обновлен пользователь автоматических отметок: {UserId}",
                    user.Id
                );

                result = Ok(
                    OperationResult<DailyAutoMarkupUser?>.Ok("Пользователь успешно обновлен", user)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении пользователя {UserId}", id);
            result = Ok(
                OperationResult<DailyAutoMarkupUser?>.Bad(
                    "Ошибка при обновлении пользователя",
                    null
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Удалить пользователя
    /// </summary>
    [HttpDelete("users/{id:guid}")]
    public async Task<ActionResult<OperationResult>> DeleteUser(Guid id)
    {
        ActionResult<OperationResult> result = null!;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var user = await dbContext.HonkaiMarkupUser.FindAsync(id);

            if (user == null)
            {
                result = Ok(OperationResult.Bad($"Пользователь с ID {id} не найден"));
            }
            else
            {
                dbContext.HonkaiMarkupUser.Remove(user);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Удален пользователь автоматических отметок: {UserId}", id);

                result = Ok(OperationResult.Ok("Пользователь успешно удален"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении пользователя {UserId}", id);
            result = Ok(OperationResult.Bad("Ошибка при удалении пользователя"));
        }

        return result;
    }

    /// <summary>
    /// Принудительно активировать ежедневные отметки для пользователя
    /// </summary>
    [HttpPost("users/{id:guid}/redeem-now")]
    public async Task<ActionResult<OperationResult>> RedeemNow(Guid id)
    {
        ActionResult<OperationResult> result = null!;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var user = await dbContext.HonkaiMarkupUser.FindAsync(id);

            if (user == null)
            {
                result = Ok(OperationResult.Bad($"Пользователь с ID {id} не найден"));
            }
            else
            {
                // Сбрасываем время последней отметки, чтобы сервис мог снова активировать отметки
                user.LastAutoMarkup = DateTime.UtcNow.AddDays(-1);
                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Сброшено время последней отметки для пользователя {UserId}",
                    id
                );

                result = Ok(
                    OperationResult.Ok(
                        "Время последней отметки сброшено. Отметки будут активированы при следующей проверке."
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при сбросе времени отметки для пользователя {UserId}", id);
            result = Ok(OperationResult.Bad("Ошибка при сбросе времени отметки"));
        }

        return result;
    }

    /// <summary>
    /// Получить статистику пользователей
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<OperationResult<object>>> GetStats()
    {
        ActionResult<OperationResult<object>> result = null!;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

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

            result = Ok(OperationResult<object>.Ok("Получена статистика пользователей", stats));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статистики");
            result = Ok(OperationResult<object>.Bad("Ошибка при получении статистики", new { }));
        }

        return result;
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
