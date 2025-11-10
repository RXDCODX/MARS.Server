using MARS.Server.Services;
using MARS.Server.Services.EnvironmentVariable;
using Microsoft.AspNetCore.Mvc;
using EnvironmentVariableEntity = MARS.Server.Services.EnvironmentVariable.Entitys.EnvironmentVariable;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для управления переменными окружения
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EnvironmentVariableController(
    EnvironmentVariableService service,
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
            var variables = await service.GetAllVariablesAsync(cancellationToken);
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
                var variable = await service.GetVariableAsync(key, cancellationToken);
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
                var operationResult = await service.SetVariableAsync(
                    request.Key,
                    request.Value ?? string.Empty,
                    request.Description,
                    cancellationToken
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
                var operationResult = await service.DeleteVariableAsync(key, cancellationToken);
                result = Ok(operationResult);
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
            await service.LoadEnvironmentVariablesFromDatabaseAsync(cancellationToken);
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
