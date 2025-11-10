using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.EnvironmentVariable;

namespace MARS.Server.Services.CommandExecutor.Commands;

/// <summary>
/// Команда для установки переменных окружения
/// </summary>
public class SetEnvCommand(EnvironmentVariableService environmentVariableService) : BaseCommand
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
        var result = string.Empty;

        var hasKey = parameters.TryGetValue("key", out var keyObj) && keyObj != null;
        var hasValue = parameters.TryGetValue("value", out var valueObj) && valueObj != null;
        var hasDescription =
            parameters.TryGetValue("description", out var descObj) && descObj != null;

        if (!hasKey && !hasValue && !hasDescription)
        {
            await environmentVariableService.LoadEnvironmentVariablesFromDatabaseAsync(
                cancellationToken
            );
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

                    var operationResult = await environmentVariableService.SetVariableAsync(
                        key,
                        value,
                        description,
                        cancellationToken
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
