namespace MARS.Server.Services.CommandExecutor.Entitys.Commands;

public abstract class BaseCommand
{
    public abstract string CommandName { get; }
    public abstract string Description { get; }
    public abstract bool IsAdminCommand { get; }

    /// <summary>
    /// Список доступных платформ для команды. По умолчанию доступна везде.
    /// </summary>
    public virtual Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Discord, Platform.Vk, Platform.Twitch];

    /// <summary>
    /// Список алиасов команды
    /// </summary>
    public virtual string[] Aliases => [];

    /// <summary>
    /// Параметры команды
    /// </summary>
    public virtual CommandParameterInfo[] Parameters => [];

    /// <summary>
    /// Флаги видимости команды в различных контекстах
    /// </summary>
    public virtual CommandVisibility Visibility => CommandVisibility.All;

    /// <summary>
    /// Выполняет команду с разобранными параметрами
    /// </summary>
    /// <param name="parameters">Разобранные параметры</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <param name="platform">Платформа выполнения (по умолчанию General)</param>
    /// <returns>Результат выполнения команды</returns>
    public abstract Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Разбирает входную строку на параметры согласно параметрам команды
    /// </summary>
    /// <param name="input">Входная строка</param>
    /// <returns>Словарь параметров</returns>
    public virtual Dictionary<string, object> ParseParameters(string input)
    {
        var parameters = new Dictionary<string, object>();
        var commandParameters = Parameters;

        if (string.IsNullOrWhiteSpace(input))
        {
            // Добавляем значения по умолчанию для необязательных параметров
            foreach (
                var param in commandParameters.Where(p =>
                    p is { Required: false, DefaultValue: not null }
                )
            )
            {
                parameters[param.Name] = ConvertValue(param.DefaultValue!, param.Type);
            }
            return parameters;
        }

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentIndex = 0;

        foreach (var param in commandParameters)
        {
            if (currentIndex >= parts.Length)
            {
                if (param.Required)
                {
                    throw new ArgumentException($"Обязательный параметр '{param.Name}' не указан");
                }
                if (param.DefaultValue != null)
                {
                    parameters[param.Name] = ConvertValue(param.DefaultValue, param.Type);
                }
                continue;
            }

            // Если это последний параметр и он может содержать пробелы, берем все оставшиеся части
            if (param.Type == "string" && param == commandParameters.Last())
            {
                var remainingParts = parts.Skip(currentIndex);
                parameters[param.Name] = string.Join(" ", remainingParts);
                break;
            }

            parameters[param.Name] = ConvertValue(parts[currentIndex], param.Type);
            currentIndex++;
        }

        return parameters;
    }

    /// <summary>
    /// Получает информацию о параметрах команды для фронтенда
    /// </summary>
    /// <returns>Информация о параметрах</returns>
    public virtual CommandParameterInfo[] GetParameterInfo()
    {
        return Parameters;
    }

    /// <summary>
    /// Получает список доступных платформ для команды
    /// </summary>
    /// <returns>Список платформ</returns>
    public virtual Platform[] GetAvailablePlatforms()
    {
        return AvailablePlatforms;
    }

    /// <summary>
    /// Проверяет, доступна ли команда на указанной платформе
    /// </summary>
    /// <param name="platform">Платформа</param>
    /// <returns>True, если команда доступна</returns>
    public virtual bool IsAvailableOnPlatform(Platform platform)
    {
        var availablePlatforms = GetAvailablePlatforms();
        return availablePlatforms.Contains(platform);
    }

    /// <summary>
    /// Проверяет, должна ли команда отображаться в указанном контексте
    /// </summary>
    /// <param name="visibility">Контекст видимости</param>
    /// <returns>True, если команда должна отображаться</returns>
    public virtual bool IsVisibleIn(CommandVisibility visibility)
    {
        return (Visibility & visibility) != 0;
    }

    private static object ConvertValue(string value, string type)
    {
        return type.ToLower() switch
        {
            "int" => int.Parse(value),
            "long" => long.Parse(value),
            "double" => double.Parse(value),
            "bool" => value.Equals("true", StringComparison.OrdinalIgnoreCase),
            "string" => value,
            _ => value,
        };
    }
}

public class CommandParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; } = true;
    public string? DefaultValue { get; set; }
}
