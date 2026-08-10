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
    /// Включена ли поддержка inline-режима для команды
    /// </summary>
    public virtual bool SupportsInline => false;

    /// <summary>
    /// Включена ли поддержка media-inline (photo/gif/video)
    /// </summary>
    public virtual bool SupportsMediaInline => false;

    /// <summary>
    /// Предоставляет URL превью/контента для media-inline (опционально)
    /// </summary>
    public virtual string? InlinePreviewUrl => null;

    /// <summary>
    /// Заголовок для inline-результата
    /// </summary>
    public virtual string InlineTitle => CommandName;

    /// <summary>
    /// Описание для inline-результата
    /// </summary>
    public virtual string InlineDescription => Description;

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

        var parts = ParseParametersWithQuotes(input);
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

            // Если это последний параметр типа string и он последний в списке,
            // и мы еще не использовали все части, то берем все оставшиеся части
            // (это позволяет передавать параметры с пробелами без кавычек, если это последний параметр)
            if (
                param.Type == "string"
                && param == commandParameters.Last()
                && currentIndex < parts.Length - 1
            )
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
    /// Разбирает строку на параметры с поддержкой кавычек
    /// Текст в кавычках (одинарных или двойных) воспринимается как один параметр
    /// </summary>
    /// <param name="input">Входная строка</param>
    /// <returns>Массив параметров</returns>
    public static string[] ParseParametersWithQuotes(string input)
    {
        var result = new List<string>();
        var currentPart = new System.Text.StringBuilder();
        var inQuotes = false;
        char? quoteChar = null;
        var i = 0;

        while (i < input.Length)
        {
            var currentChar = input[i];

            if (!inQuotes)
            {
                if (currentChar == '"' || currentChar == '\'')
                {
                    inQuotes = true;
                    quoteChar = currentChar;
                }
                else if (char.IsWhiteSpace(currentChar))
                {
                    if (currentPart.Length > 0)
                    {
                        result.Add(currentPart.ToString());
                        currentPart.Clear();
                    }
                }
                else
                {
                    currentPart.Append(currentChar);
                }
            }
            else
            {
                if (currentChar == quoteChar)
                {
                    // Проверяем, не экранирована ли кавычка
                    if (i + 1 < input.Length && input[i + 1] == quoteChar)
                    {
                        // Экранированная кавычка (двойная кавычка внутри кавычек)
                        currentPart.Append(quoteChar);
                        i++; // Пропускаем следующую кавычку
                    }
                    else
                    {
                        // Закрывающая кавычка
                        inQuotes = false;
                        quoteChar = null;
                        result.Add(currentPart.ToString());
                        currentPart.Clear();
                    }
                }
                else if (currentChar == '\\' && i + 1 < input.Length)
                {
                    // Обработка escape-последовательностей
                    var nextChar = input[i + 1];
                    if (nextChar == '\\' || nextChar == '"' || nextChar == '\'')
                    {
                        currentPart.Append(nextChar);
                        i++; // Пропускаем следующий символ
                    }
                    else
                    {
                        currentPart.Append(currentChar);
                    }
                }
                else
                {
                    currentPart.Append(currentChar);
                }
            }

            i++;
        }

        // Добавляем последний параметр, если он есть
        if (currentPart.Length > 0)
        {
            result.Add(currentPart.ToString());
        }

        return result.ToArray();
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
        return Enumerable.Contains(availablePlatforms, platform);
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
