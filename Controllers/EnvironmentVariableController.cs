using MARS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using EnvironmentVariableEntity = MARS.Server.Services.EnvironmentVariable.Entitys.EnvironmentVariable;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для управления переменными окружения
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EnvironmentVariableController(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<EnvironmentVariableController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить все переменные окружения
    /// </summary>
    [HttpGet]
    public async Task<
        ActionResult<OperationResult<List<EnvironmentVariableEntity>>>
    > GetAllVariables(CancellationToken cancellationToken = default)
    {
        ActionResult<OperationResult<List<EnvironmentVariableEntity>>> result;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var variables = await dbContext
                .EnvironmentVariables.AsNoTracking()
                .ToListAsync(cancellationToken);
            result = Ok(
                OperationResult<List<EnvironmentVariableEntity>>.Ok(
                    "Переменные окружения получены",
                    variables
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении переменных окружения");
            result = Ok(
                OperationResult<List<EnvironmentVariableEntity>>.Bad(
                    "Ошибка при получении переменных окружения",
                    []
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить переменную окружения по ключу
    /// </summary>
    [HttpGet("{key}")]
    public async Task<ActionResult<OperationResult<EnvironmentVariableEntity?>>> GetVariable(
        [FromRoute] string key,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<EnvironmentVariableEntity?>> result;

        try
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                result = Ok(
                    OperationResult<EnvironmentVariableEntity?>.Bad(
                        "Ключ переменной окружения не может быть пустым",
                        null
                    )
                );
            }
            else
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var variable = await dbContext.EnvironmentVariables.FindAsync([key], cancellationToken);

                if (variable is null)
                {
                    variable = new EnvironmentVariableEntity { Key = key };
                    await dbContext.EnvironmentVariables.AddAsync(variable, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                if (variable is not null)
                {
                    result = Ok(
                        OperationResult<EnvironmentVariableEntity?>.Ok(
                            "Переменная окружения найдена",
                            variable
                        )
                    );
                }
                else
                {
                    result = Ok(
                        OperationResult<EnvironmentVariableEntity?>.Bad(
                            "Переменная окружения не найдена",
                            null
                        )
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении переменной окружения: Key={Key}", key);
            result = Ok(
                OperationResult<EnvironmentVariableEntity?>.Bad(
                    "Ошибка при получении переменной окружения",
                    null
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Установить или обновить переменную окружения
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OperationResult>> SetVariable(
        [FromBody] SetEnvironmentVariableRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                result = Ok(OperationResult.Bad("Ключ переменной окружения не может быть пустым"));
            }
            else
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

                var key = request.Key;
                var value = request.Value ?? string.Empty;
                var variable = await dbContext.EnvironmentVariables.FirstOrDefaultAsync(
                    e => e.Key == key,
                    cancellationToken
                );

                if (variable is not null)
                {
                    variable.Value = value;
                    variable.Description = request.Description;
                    variable.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    variable = new EnvironmentVariableEntity
                    {
                        Key = key,
                        Value = value,
                        Description = request.Description,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    await dbContext.EnvironmentVariables.AddAsync(variable, cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                Environment.SetEnvironmentVariable(key, value);

                var operationResult = OperationResult.Ok(
                    "Переменная окружения успешно установлена"
                );
                result = Ok(operationResult);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при установке переменной окружения: Key={Key}",
                request.Key
            );
            result = Ok(OperationResult.Bad("Ошибка при установке переменной окружения"));
        }

        return result;
    }

    /// <summary>
    /// Удалить переменную окружения
    /// </summary>
    [HttpDelete("{key}")]
    public async Task<ActionResult<OperationResult>> DeleteVariable(
        [FromRoute] string key,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                result = Ok(OperationResult.Bad("Ключ переменной окружения не может быть пустым"));
            }
            else
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var variable = await dbContext.EnvironmentVariables.FirstOrDefaultAsync(
                    e => e.Key == key,
                    cancellationToken
                );

                if (variable is null)
                {
                    result = Ok(OperationResult.Bad("Переменная окружения не найдена"));
                }
                else
                {
                    variable.Value = null;
                    variable.UpdatedAt = DateTime.UtcNow;
                    dbContext.EnvironmentVariables.Update(variable);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    Environment.SetEnvironmentVariable(key, null);

                    var operationResult = OperationResult.Ok(
                        "Переменная окружения успешно удалена"
                    );
                    result = Ok(operationResult);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении переменной окружения: Key={Key}", key);
            result = Ok(OperationResult.Bad("Ошибка при удалении переменной окружения"));
        }

        return result;
    }

    /// <summary>
    /// Перезагрузить переменные окружения из базы данных
    /// </summary>
    [HttpPost("reload")]
    public async Task<ActionResult<OperationResult>> ReloadVariables(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var variables = await dbContext
                .EnvironmentVariables.AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var variable in variables)
            {
                if (!string.IsNullOrWhiteSpace(variable.Key))
                {
                    Environment.SetEnvironmentVariable(variable.Key, variable.Value);
                }
            }

            result = Ok(OperationResult.Ok("Переменные окружения успешно перезагружены"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при перезагрузке переменных окружения");
            result = Ok(OperationResult.Bad("Ошибка при перезагрузке переменных окружения"));
        }

        return result;
    }
}

/// <summary>
/// Запрос на установку переменной окружения
/// </summary>
public class SetEnvironmentVariableRequest
{
    /// <summary>
    /// Ключ переменной окружения
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Значение переменной окружения
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Описание переменной
    /// </summary>
    public string? Description { get; set; }
}
