using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using MARS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для управления глобальным состоянием приложения (RootState)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RootStateController(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<RootStateController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить все state-переменные
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<OperationResult<List<RootState>>>> GetAllKeys(
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<RootState>>.Bad("Ошибка при получении состояния");

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var keys = await dbContext.RootState.AsNoTracking().ToListAsync(cancellationToken);
            result = OperationResult<List<RootState>>.Ok("Состояние получено", keys);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении всех state-переменных");
            result = OperationResult<List<RootState>>.Bad(
                $"Ошибка при получении состояния: {ex.Message}",
                []
            );
        }

        return Ok(result);
    }

    /// <summary>
    /// Получить state-переменную по имени
    /// </summary>
    [HttpGet("{name}")]
    public async Task<ActionResult<OperationResult<RootState?>>> GetKey(
        [FromRoute] string name,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<RootState?>.Bad("Ошибка при получении переменной");

        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );
                var state = await dbContext
                    .RootState.AsNoTracking()
                    .SingleOrDefaultAsync(s => s.Name == name, cancellationToken);

                if (state is not null)
                {
                    result = OperationResult<RootState?>.Ok("Переменная найдена", state);
                }
                else
                {
                    result = OperationResult<RootState?>.Bad(
                        $"Переменная с именем '{name}' не найдена",
                        null
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при получении переменной {Name}", name);
                result = OperationResult<RootState?>.Bad(
                    $"Ошибка при получении переменной: {ex.Message}",
                    null
                );
            }
        }
        else
        {
            result = OperationResult<RootState?>.Bad("Имя переменной не может быть пустым", null);
        }

        return Ok(result);
    }

    /// <summary>
    /// Создать или обновить state-переменную
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OperationResult<RootState>>> UpsertKey(
        [FromBody] RootState request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<RootState>.Bad("Ошибка при сохранении переменной");

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );

                var existingState = await dbContext.RootState.SingleOrDefaultAsync(
                    s => s.Name == request.Name,
                    cancellationToken
                );

                if (existingState is not null)
                {
                    existingState.Value = request.Value;
                    existingState.Description = request.Description;
                    existingState.TypeDescription = request.TypeDescription;
                    dbContext.RootState.Update(existingState);
                }
                else
                {
                    await dbContext.RootState.AddAsync(request, cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                result = OperationResult<RootState>.Ok(
                    existingState is not null
                        ? $"Переменная '{request.Name}' обновлена"
                        : $"Переменная '{request.Name}' создана",
                    request
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при сохранении переменной {Name}", request.Name);
                result = OperationResult<RootState>.Bad(
                    $"Ошибка при сохранении переменной: {ex.Message}"
                );
            }
        }
        else
        {
            result = OperationResult<RootState>.Bad("Имя переменной не может быть пустым");
        }

        return Ok(result);
    }

    /// <summary>
    /// Обновить значение существующей state-переменной
    /// </summary>
    [HttpPatch("{name}/value")]
    public async Task<ActionResult<OperationResult<RootState>>> UpdateValue(
        [FromRoute] string name,
        [FromBody] UpdateValueRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<RootState>.Bad("Ошибка при обновлении значения");

        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );

                var state = await dbContext.RootState.SingleOrDefaultAsync(
                    s => s.Name == name,
                    cancellationToken
                );

                if (state is not null)
                {
                    state.Value = request.Value ?? string.Empty;
                    dbContext.RootState.Update(state);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    result = OperationResult<RootState>.Ok(
                        $"Значение переменной '{name}' обновлено",
                        state
                    );
                }
                else
                {
                    result = OperationResult<RootState>.Bad(
                        $"Переменная с именем '{name}' не найдена"
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при обновлении значения переменной {Name}", name);
                result = OperationResult<RootState>.Bad(
                    $"Ошибка при обновлении значения: {ex.Message}"
                );
            }
        }
        else
        {
            result = OperationResult<RootState>.Bad("Имя переменной не может быть пустым");
        }

        return Ok(result);
    }

    /// <summary>
    /// Удалить state-переменную
    /// </summary>
    [HttpDelete("{name}")]
    public async Task<ActionResult<OperationResult>> DeleteKey(
        [FromRoute] string name,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult.Bad("Ошибка при удалении переменной");

        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );

                var state = await dbContext.RootState.SingleOrDefaultAsync(
                    s => s.Name == name,
                    cancellationToken
                );

                if (state is not null)
                {
                    dbContext.RootState.Remove(state);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    result = OperationResult.Ok($"Переменная '{name}' удалена");
                }
                else
                {
                    result = OperationResult.Bad($"Переменная с именем '{name}' не найдена");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при удалении переменной {Name}", name);
                result = OperationResult.Bad($"Ошибка при удалении переменной: {ex.Message}");
            }
        }
        else
        {
            result = OperationResult.Bad("Имя переменной не может быть пустым");
        }

        return Ok(result);
    }
}

/// <summary>
/// Модель запроса для обновления значения переменной
/// </summary>
public record UpdateValueRequest
{
    /// <summary>
    /// Новое значение переменной
    /// </summary>
    public required string Value { get; init; }
}
