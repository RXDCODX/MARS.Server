using MARS.Server.DataBaseContext;
using MARS.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using EnvironmentVariableEntity = MARS.Server.Services.EnvironmentVariable.Entitys.EnvironmentVariable;

namespace MARS.Server.Services.EnvironmentVariable;

/// <summary>
/// Сервис для управления переменными окружения, хранимыми в базе данных
/// </summary>
public class EnvironmentVariableService : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<EnvironmentVariableService> _logger;

    public EnvironmentVariableService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<EnvironmentVariableService> logger
    )
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Загрузка переменных окружения из базы данных...");
            await LoadEnvironmentVariablesFromDatabaseAsync(stoppingToken);
            _logger.LogInformation("Переменные окружения успешно загружены из базы данных");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке переменных окружения из базы данных");
        }
    }

    /// <summary>
    /// Загружает все переменные окружения из базы данных и устанавливает их в Environment
    /// </summary>
    public async Task LoadEnvironmentVariablesFromDatabaseAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var variables = await dbContext
            .EnvironmentVariables.AsNoTracking()
            .ToListAsync(cancellationToken);

        if (!variables.Any())
        {
            _logger.LogInformation("В базе данных нет переменных окружения");
            return;
        }

        foreach (var variable in variables)
        {
            if (!string.IsNullOrWhiteSpace(variable.Key))
            {
                System.Environment.SetEnvironmentVariable(variable.Key, variable.Value);
                _logger.LogInformation(
                    "Переменная окружения установлена: {Key} = {Value}",
                    variable.Key,
                    variable.Value != null && variable.Value.Length > 0 ? "***" : "(пусто)"
                );
            }
        }

        _logger.LogInformation("Загружено переменных окружения: {Count}", variables.Count);
    }

    /// <summary>
    /// Получает все переменные окружения из базы данных
    /// </summary>
    public async Task<List<EnvironmentVariableEntity>> GetAllVariablesAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

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

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext
            .EnvironmentVariables.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Key == key, cancellationToken);
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

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var existingVariable = await dbContext.EnvironmentVariables.FirstOrDefaultAsync(
            v => v.Key == key,
            cancellationToken
        );

        if (existingVariable != null)
        {
            existingVariable.Value = value ?? string.Empty;
            existingVariable.Description = description;
            existingVariable.UpdatedAt = DateTime.UtcNow;
            dbContext.EnvironmentVariables.Update(existingVariable);
        }
        else
        {
            var newVariable = new EnvironmentVariableEntity
            {
                Key = key,
                Value = value ?? string.Empty,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await dbContext.EnvironmentVariables.AddAsync(newVariable, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Обновляем переменную в Environment
        System.Environment.SetEnvironmentVariable(key, value);

        _logger.LogInformation(
            "Переменная окружения установлена: {Key} = {Value}",
            key,
            value != null && value.Length > 0 ? "***" : "(пусто)"
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

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var variable = await dbContext.EnvironmentVariables.FirstOrDefaultAsync(
            v => v.Key == key,
            cancellationToken
        );

        if (variable == null)
        {
            return OperationResult.Bad("Переменная окружения не найдена");
        }

        dbContext.EnvironmentVariables.Remove(variable);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Удаляем переменную из Environment (устанавливаем null)
        System.Environment.SetEnvironmentVariable(key, null);

        _logger.LogInformation("Переменная окружения удалена: {Key}", key);

        return OperationResult.Ok("Переменная окружения успешно удалена");
    }
}
