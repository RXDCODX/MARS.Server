using EnvironmentVariableEntity = MARS.Server.Services.EnvironmentVariable.Entitys.EnvironmentVariable;

namespace MARS.Server.Services.EnvironmentVariable;

/// <summary>
/// Сервис для управления переменными окружения, хранимыми в базе данных
/// </summary>
public class EnvironmentVariableService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<EnvironmentVariableService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Загрузка переменных окружения из базы данных...");
            await LoadEnvironmentVariablesFromDatabaseAsync(stoppingToken);
            logger.LogInformation("Переменные окружения успешно загружены из базы данных");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при загрузке переменных окружения из базы данных");
        }
    }

    /// <summary>
    /// Загружает все переменные окружения из базы данных и устанавливает их в Environment
    /// </summary>
    public async Task LoadEnvironmentVariablesFromDatabaseAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var variables = await dbContext
            .EnvironmentVariables.AsNoTracking()
            .ToListAsync(cancellationToken);

        if (variables.Count == 0)
        {
            logger.LogInformation("В базе данных нет переменных окружения");
            return;
        }

        foreach (var variable in variables)
        {
            if (!string.IsNullOrWhiteSpace(variable.Key))
            {
                Environment.SetEnvironmentVariable(variable.Key, variable.Value);
                logger.LogInformation(
                    "Переменная окружения установлена: {Key} = {Value}",
                    variable.Key,
                    variable.Value is { Length: > 0 } ? "***" : "(пусто)"
                );
            }
        }

        logger.LogInformation("Загружено переменных окружения: {Count}", variables.Count);
    }

    /// <summary>
    /// Получает все переменные окружения из базы данных
    /// </summary>
    public async Task<List<EnvironmentVariableEntity>> GetAllVariablesAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.EnvironmentVariables.AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Получает переменную окружения по ключу
    /// </summary>
    public async Task<EnvironmentVariableEntity?> GetVariableAsync(
        string key,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var variable = await dbContext.EnvironmentVariables.FindAsync([key], cancellationToken);

        if (variable is null)
        {
            variable = new EnvironmentVariableEntity { Key = key };
            await dbContext.EnvironmentVariables.AddAsync(variable, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return variable;
    }

    /// <summary>
    /// Устанавливает или обновляет переменную окружения
    /// </summary>
    public async Task<OperationResult> SetVariableAsync(
        string key,
        string value,
        string? description = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return OperationResult.Bad("Ключ переменной окружения не может быть пустым");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingVariable = await dbContext.EnvironmentVariables.FirstOrDefaultAsync(
            v => v.Key == key,
            cancellationToken
        );

        if (existingVariable != null)
        {
            existingVariable.Value = value;
            existingVariable.Description = description;
            existingVariable.UpdatedAt = DateTime.UtcNow;
            dbContext.EnvironmentVariables.Update(existingVariable);
        }
        else
        {
            var newVariable = new EnvironmentVariableEntity
            {
                Key = key,
                Value = value,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await dbContext.EnvironmentVariables.AddAsync(newVariable, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Обновляем переменную в Environment
        Environment.SetEnvironmentVariable(key, value);

        logger.LogInformation(
            "Переменная окружения установлена: {Key} = {Value}",
            key,
            value is { Length: > 0 } ? "***" : "(пусто)"
        );

        return OperationResult.Ok("Переменная окружения успешно установлена");
    }

    /// <summary>
    /// Удаляет переменную окружения
    /// </summary>
    public async Task<OperationResult> DeleteVariableAsync(
        string key,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return OperationResult.Bad("Ключ переменной окружения не может быть пустым");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var variable = await dbContext.EnvironmentVariables.FirstOrDefaultAsync(
            v => v.Key == key,
            cancellationToken
        );

        if (variable == null)
        {
            return OperationResult.Bad("Переменная окружения не найдена");
        }

        variable.Value = null;

        dbContext.EnvironmentVariables.Update(variable);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Удаляем переменную из Environment (устанавливаем null)
        Environment.SetEnvironmentVariable(key, null);

        logger.LogInformation("Переменная окружения удалена: {Key}", key);

        return OperationResult.Ok("Переменная окружения успешно удалена");
    }
}
