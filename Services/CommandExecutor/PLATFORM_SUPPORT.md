# Поддержка платформ в системе команд

## Обзор

Система команд теперь поддерживает выполнение на разных платформах с вариативными ответами и ограничениями.

## Архитектура

### Основные компоненты

1. **Platform** - enum для определения платформ
2. **IPlatformCommandService** - интерфейс для платформенных сервисов
3. **PlatformCommandServiceBase** - базовый класс для платформенных сервисов
4. **PlatformCommandManager** - менеджер для управления платформенными командами
5. **Конкретные реализации** - TelegramCommandService, TwitchCommandService и др.

### Поддерживаемые платформы

- **General** - общая платформа (по умолчанию)
- **Telegram** - Telegram Bot API (лимит 4096 символов)
- **Twitch** - Twitch Chat (лимит 500 символов)
- **Discord** - Discord Bot API (лимит 2000 символов)

## Использование

### 1. Создание команды с поддержкой платформ

```csharp
[Description("Описание команды")]
[Alias("alias")]
public class MyCommand : BaseCommand
{
    public override string CommandName => "mycommand";
    public override string Description => "Описание команды";
    public override bool IsAdminCommand => false;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default,
        Platform platform = Platform.General
    )
    {
        // Разные ответы для разных платформ
        return platform switch
        {
            Platform.Telegram => "Ответ для Telegram с Markdown",
            Platform.Twitch => "Короткий ответ для Twitch",
            Platform.Discord => "Ответ для Discord",
            _ => "Общий ответ"
        };
    }
}
```

### 2. Регистрация сервисов

```csharp
// В Program.cs или Startup.cs
services.AddCommandExecutorServices();
```

### 3. Использование в контроллере

```csharp
[HttpPost("execute")]
public async Task<IActionResult> ExecuteCommand([FromBody] ExecuteCommandRequest request)
{
    var result = await commandService.ExecuteCommandAsync(
        request.CommandName,
        request.Input ?? "",
        cancellationToken,
        request.Platform
    );

    return Ok(new ExecuteCommandResponse { Result = result, Success = true });
}
```

## API Endpoints

### Выполнение команды
```
POST /api/commands/execute
{
    "commandName": "platformtest",
    "input": "",
    "platform": "Telegram"
}
```

### Получение списка команд для платформы
```
GET /api/commands/info/Telegram
```

## Ограничения платформ

### Telegram
- Максимальная длина: 4096 символов
- Поддержка: Markdown, эмодзи
- Автоматическая обрезка с индикатором

### Twitch
- Максимальная длина: 500 символов
- Поддержка: Эмодзи, базовое форматирование
- Короткая обрезка

### Discord
- Максимальная длина: 2000 символов
- Поддержка: Markdown, эмодзи, вложения

## Добавление новой платформы

1. Создайте новый сервис, наследующий от `PlatformCommandServiceBase`
2. Реализуйте необходимые методы
3. Зарегистрируйте сервис в `CommandExecutorServiceCollectionExtensions`
4. Добавьте платформу в enum `Platform`

```csharp
public class NewPlatformCommandService : PlatformCommandServiceBase
{
    public override Platform Platform => Platform.NewPlatform;
    
    protected override int DefaultMaxResponseLength => 1000;
    
    protected override IEnumerable<string> AvailableCommands => new[]
    {
        "command1",
        "command2"
    };
}
```

## Тестирование

Используйте команду `platformtest` для проверки работы системы:

```
POST /api/commands/execute
{
    "commandName": "platformtest",
    "platform": "Telegram"
}
```

Эта команда покажет разные ответы для разных платформ с соответствующими ограничениями. 