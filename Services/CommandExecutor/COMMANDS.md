# Создание команд на базе `BaseCommand`

`BaseCommand` находится в пространстве имён `MARS.Server.Services.CommandExecutor.Entitys.Commands` и задаёт контракт для всех команд. Ниже — минимальный чек-лист, правила парсинга и примеры.

## Обязательные переопределения
- `CommandName` — имя команды (без префиксов).
- `Description` — краткое описание для помощи/справки.
- `IsAdminCommand` — флаг доступности только для админов.
- `ExecuteAsync(Dictionary<string, object> parameters, Platform platform, CancellationToken cancellationToken)` — основная логика.

## Дополнительные опции (необязательные)
- `AvailablePlatforms` — платформа(ы), где команда доступна. По умолчанию: `Telegram`, `Api`, `Discord`, `Vk`, `Twitch`.
- `Aliases` — альтернативные имена.
- `Parameters` — описание параметров (см. ниже `CommandParameterInfo`).
- `Visibility` — флаги `CommandVisibility`, где команда отображается.
- `ParseParameters` — при необходимости можно переопределить логику парсинга.

## Параметры (`CommandParameterInfo`)
Поле `Parameters` возвращает массив `CommandParameterInfo` со свойствами:
- `Name` — ключ, под которым параметр придёт в `ExecuteAsync`.
- `Description` — описание для UI/хелпа.
- `Type` — `string` | `int` | `long` | `double` | `bool` (используется в конвертации).
- `Required` — обязательность.
- `DefaultValue` — строковое значение по умолчанию (для необязательных параметров автоматически конвертируется и подставляется).

## Правила парсинга по умолчанию (`BaseCommand.ParseParameters`)
- Поддерживаются кавычки `'` и `"`; текст в кавычках считается одним параметром.
- Экранирование кавычек и `\` внутри кавычек поддерживается.
- Если входная строка пустая, необязательные параметры с `DefaultValue` автоматически подставляются.
- Если последний параметр типа `string` и он последний в списке — в него собираются все оставшиеся части (можно вводить текст с пробелами без кавычек).
- Для `bool` значение `true/false` (регистр не важен); числа парсятся через `int.Parse`, `long.Parse`, `double.Parse`.

## Минимальный шаблон
```csharp
public class MySimpleCommand : BaseCommand
{
    public override string CommandName => "simple";
    public override string Description => "Пример простой команды";
    public override bool IsAdminCommand => false;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Простой ответ");
    }
}
```

## Пример без параметров — `ByebyeCommand`
Файл: `MARS.Projects/MARS.Server/Services/CommandExecutor/Commands/ByebyeCommand.cs`
```csharp
public class ByebyeCommand : BaseCommand
{
    public override string CommandName => "byebye";
    public override string Description => "Прощание с пользователем";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Discord, Platform.Vk, Platform.Twitch];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default)
    {
        const string usage = """
            Пока-пока! 👋
            Надеюсь, ты скоро вернешься! 😊
            """;

        return Task.FromResult(usage);
    }
}
```
Особенности: `Parameters` и `Aliases` не нужны, платформа — везде, логика — просто вернуть строку.

## Пример с параметрами — `HelloVideoCommand`
Файл: `MARS.Projects/MARS.Server/Services/CommandExecutor/Commands/HelloVideoCommand.cs`
```csharp
public class HelloVideoCommand(HelloVideoWorker helloVideoWorker) : BaseCommand
{
    public override string CommandName => "hellovideo";
    public override string Description =>
        "Отправляет приветственное видео пользователю или с указанным цветом";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

    public override CommandParameterInfo[] Parameters =>
    [
        new() { Name = "name", Description = "Имя пользователя", Type = "string", Required = true },
        new() { Name = "color", Description = "Цвет (опционально)", Type = "string", Required = false },
    ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.TryGetValue("name", out var nameObj))
        {
            return "Необходимо указать имя пользователя";
        }

        var name = nameObj.ToString() ?? "";
        name = name.StartsWith('@') ? name[1..] : name;
        var color = parameters.TryGetValue("color", out var colorObj) ? colorObj?.ToString() : null;

        string? resultName;
        if (!string.IsNullOrWhiteSpace(color))
        {
            resultName = await helloVideoWorker.TestVideo(name, color);
            if (resultName != null)
            {
                return $"Отправл приветствующий видос на имя {resultName} с цветом {color}";
            }
        }
        else
        {
            resultName = await helloVideoWorker.TestVideo(name);
            if (resultName != null)
            {
                return $"Отправл приветствующий видос на имя {resultName}";
            }
        }

        return "Кривые параметры";
    }
}
```
Особенности: обязательный `name`, необязательный `color`; использование `TryGetValue`, простая валидация, различное поведение по платформе не требуется — но платформа ограничена тремя.

## Быстрый чек-лист при создании новой команды
1. Задайте `CommandName`, `Description`, `IsAdminCommand`.
2. При необходимости: `AvailablePlatforms`, `Visibility`, `Aliases`.
3. Опишите `Parameters` (тип, обязательность, `DefaultValue`).
4. В `ExecuteAsync` доставайте параметры по ключам, учитывайте `platform` и поддерживайте `CancellationToken` в асинхронных вызовах.
5. Если нужен сложный парсинг — переопределите `ParseParameters`; иначе используйте базовый (кавычки, экранирование, последний строковый параметр с пробелами уже поддержаны).
6. Зарегистрируйте новую команду в DI/реестре команд (см. `CommandExecutorServiceCollectionExtensions` / `CommandFactory`).
