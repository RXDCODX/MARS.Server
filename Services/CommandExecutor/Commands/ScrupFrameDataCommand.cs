using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Framedata;
using MARS.Server.Services.Framedata.Subservices.Entitys;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class ScrupFrameDataCommand(Tekken8FrameData frameData) : BaseCommand
{
    public override string CommandName => "scrupframedata";
    public override string Description =>
        "Запускает парсинг фреймдаты Tekken 8 с сайта с настраиваемыми параметрами";
    public override bool IsAdminCommand => true;

    public override string[] Aliases => ["scrap", "parse", "framedata"];
    public override CommandParameterInfo[] Parameters =>
        [
            new CommandParameterInfo
            {
                Name = "source",
                Description = "Источник данных: wavu или tekkendocs",
                Type = "string",
                Required = false,
                DefaultValue = "wavu",
            },
            new CommandParameterInfo
            {
                Name = "requestDelay",
                Description = "Задержка между запросами в секундах",
                Type = "int",
                Required = false,
                DefaultValue = "2",
            },
            new CommandParameterInfo
            {
                Name = "characterDelay",
                Description = "Задержка между персонажами в секундах",
                Type = "int",
                Required = false,
                DefaultValue = "5",
            },
            new CommandParameterInfo
            {
                Name = "parseMoves",
                Description = "Парсить ли мувы для персонажей",
                Type = "bool",
                Required = false,
                DefaultValue = "true",
            },
            new CommandParameterInfo
            {
                Name = "useStaging",
                Description = "Использовать ли staging service для изменений",
                Type = "bool",
                Required = false,
                DefaultValue = "true",
            },
            new CommandParameterInfo
            {
                Name = "maxRetries",
                Description = "Максимальное количество попыток для одного запроса",
                Type = "int",
                Required = false,
                DefaultValue = "3",
            },
            new CommandParameterInfo
            {
                Name = "timeout",
                Description = "Таймаут для HTTP запросов в секундах",
                Type = "int",
                Required = false,
                DefaultValue = "30",
            },
            new CommandParameterInfo
            {
                Name = "characters",
                Description = "Список персонажей через запятую (например: Kazuya,Heihachi,Jin)",
                Type = "string",
                Required = false,
                DefaultValue = null,
            },
        ];

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        // Создаем настройки парсера из параметров команды
        var options = new FramedataParserOptions
        {
            RequestDelaySeconds = GetIntParameter(parameters, "requestDelay", 2),
            CharacterDelaySeconds = GetIntParameter(parameters, "characterDelay", 5),
            UseStagingService = GetBoolParameter(parameters, "useStaging", true),
            ParseMoves = GetBoolParameter(parameters, "parseMoves", true),
            MaxRetries = GetIntParameter(parameters, "maxRetries", 3),
            HttpTimeoutSeconds = GetIntParameter(parameters, "timeout", 30),
        };

        // Получаем источник данных (по умолчанию Primary)
        var source = GetSourceParameter(parameters);

        // Получаем список персонажей для парсинга (если указан)
        var characterNames = GetCharacterNamesParameter(parameters);

        await Task.Factory.StartNew(
            async () =>
            {
                if (options.ParseMoves)
                {
                    await frameData.ParseWithCustomOptions(source, options).ConfigureAwait(false);
                }
                else
                {
                    await frameData
                        .ParseCharactersOnly(source, characterNames, options.UseStagingService)
                        .ConfigureAwait(false);
                }
            },
            cancellationToken
        );

        var optionsDescription = GetOptionsDescription(options, source, characterNames);
        return $"Парсинг запущен с параметрами:\n{optionsDescription}";
    }

    private static int GetIntParameter(
        Dictionary<string, object> parameters,
        string key,
        int defaultValue
    )
    {
        return parameters.TryGetValue(key, out var value) && value is int intValue ? intValue
            : parameters.TryGetValue(key, out var stringValue)
            && int.TryParse(stringValue.ToString(), out var parsed)
                ? parsed
            : defaultValue;
    }

    private static bool GetBoolParameter(
        Dictionary<string, object> parameters,
        string key,
        bool defaultValue
    )
    {
        if (parameters.TryGetValue(key, out var value))
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is string stringValue)
            {
                return stringValue.ToLower() switch
                {
                    "true" or "1" or "yes" or "on" => true,
                    "false" or "0" or "no" or "off" => false,
                    _ => defaultValue,
                };
            }
        }
        return defaultValue;
    }

    private static FramedataSource GetSourceParameter(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("source", out var value))
        {
            if (value is string stringValue)
            {
                return stringValue.ToLower() switch
                {
                    "wavu" => FramedataSource.Wavu,
                    "tekkendocs" => FramedataSource.Tekkendocs,
                    _ => FramedataSource.Wavu,
                };
            }
        }
        return FramedataSource.Wavu;
    }

    private static List<string>? GetCharacterNamesParameter(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("characters", out var value))
        {
            if (value is string stringValue)
            {
                return
                [
                    .. stringValue
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim())
                        .Where(c => !string.IsNullOrEmpty(c)),
                ];
            }
            if (value is List<string> listValue)
            {
                return listValue;
            }
        }
        return null;
    }

    private static string GetOptionsDescription(
        FramedataParserOptions options,
        FramedataSource source,
        List<string>? characterNames
    )
    {
        var sourceName = source switch
        {
            FramedataSource.Wavu => "Wavu.wiki",
            FramedataSource.Tekkendocs => "Tekkendocs.com",
            _ => "Неизвестный источник",
        };

        var charactersInfo =
            characterNames?.Count > 0
                ? $"Персонажи: {string.Join(", ", characterNames)}"
                : "Все персонажи";

        return $"Источник: {sourceName}\n"
            + $"{charactersInfo}\n"
            + $"Задержка между запросами: {options.RequestDelaySeconds}с\n"
            + $"Задержка между персонажами: {options.CharacterDelaySeconds}с\n"
            + $"Использовать staging: {(options.UseStagingService ? "Да" : "Нет")}\n"
            + $"Парсить мувы: {(options.ParseMoves ? "Да" : "Нет")}\n"
            + $"Макс. попыток: {options.MaxRetries}\n"
            + $"Таймаут: {options.HttpTimeoutSeconds}с";
    }
}
