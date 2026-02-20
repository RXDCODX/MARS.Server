using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using EnvironmentVariableEntity = MARS.Server.Services.EnvironmentVariable.Entitys.EnvironmentVariable;

namespace MARS.Server.Services.CommandExecutor.Commands;

/// <summary>
/// Команда для установки переменных окружения
/// </summary>
public class SetEnvCommand(IDbContextFactory<AppDbContext> dbContextFactory) : BaseCommand
{
    public override string CommandName => "setenv";
    public override string Description =>
        "Устанавливает или обновляет переменную окружения. Без параметров - перезагружает все переменные из базы данных";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Discord, Platform.Vk, Platform.Twitch];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "key",
                Description = "Ключ переменной окружения (обязательно для установки)",
                Type = "string",
                Required = false,
            },
            new()
            {
                Name = "value",
                Description = "Значение переменной окружения (обязательно для установки)",
                Type = "string",
                Required = false,
            },
            new()
            {
                Name = "description",
                Description = "Описание переменной (опционально)",
                Type = "string",
                Required = false,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        var hasKey = parameters.TryGetValue("key", out var keyObj) && keyObj != null;
        var hasValue = parameters.TryGetValue("value", out var valueObj) && valueObj != null;
        var hasDescription =
            parameters.TryGetValue("description", out var descObj) && descObj != null;

        if (!hasKey && !hasValue && !hasDescription)
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

            result = "Переменные окружения успешно перезагружены из базы данных";
        }
        else
        {
            if (!hasKey)
            {
                result = "Ошибка: ключ переменной окружения не указан";
            }
            else if (!hasValue)
            {
                result = "Ошибка: значение переменной окружения не указано";
            }
            else
            {
                var key = keyObj!.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(key))
                {
                    result = "Ошибка: ключ переменной окружения не может быть пустым";
                }
                else
                {
                    var value = valueObj!.ToString() ?? string.Empty;
                    var description = hasDescription ? descObj?.ToString() : null;

                    await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                        cancellationToken
                    );
                    var variable = await dbContext.EnvironmentVariables.FirstOrDefaultAsync(
                        e => e.Key == key,
                        cancellationToken
                    );

                    if (variable is not null)
                    {
                        variable.Value = value;
                        variable.Description = description;
                        variable.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        variable = new EnvironmentVariableEntity
                        {
                            Key = key,
                            Value = value,
                            Description = description,
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

                    if (operationResult.Success)
                    {
                        result = $"Переменная окружения '{key}' успешно установлена";
                    }
                    else
                    {
                        result =
                            $"Ошибка при установке переменной окружения: {operationResult.Message}";
                    }
                }
            }
        }

        return result;
    }
}
