# SignalR Logger

Кастомный логгер для отправки логов через SignalR в реальном времени.

## Описание

SignalR Logger позволяет отправлять логи приложения в реальном времени через SignalR соединение. Это полезно для мониторинга приложения, отладки и создания дашбордов логирования.

## Компоненты

### 1. LoggerHub

SignalR хаб, который обрабатывает подключения клиентов и отправку логов.

**Эндпоинт:** `/hubs/logger`

**Методы:**

- `SendLogMessage(LogMessageDto logMessage)` - отправляет лог всем клиентам
- `SendLogMessageToClient(string connectionId, LogMessageDto logMessage)` - отправляет лог конкретному клиенту
- `SendLogMessageToGroup(string groupName, LogMessageDto logMessage)` - отправляет лог группе клиентов
- `GetLogHistory(int count = 100)` - получает историю логов
- `ClearLogHistory()` - очищает историю логов

### 2. SignalRLogger

Кастомный `ILogger`, который перехватывает логи и отправляет их через SignalR.

### 3. SignalRLoggerProvider

Провайдер для регистрации SignalR логгера в системе логирования.

### 4. SignalRLoggerOptions

Опции конфигурации логгера.

## Конфигурация

### Регистрация в Program.cs

```csharp
// Добавляем SignalR логгер
loggingBuilder.AddSignalRLogger(options =>
{
    options.MinimumLogLevel = LogLevel.Information;
    options.SourceName = "MARS.Server";
    options.IncludeExceptions = true;
    options.IncludeStackTrace = true;
    options.MaxMessageLength = 2000;
    
    // Исключаем некоторые категории для уменьшения шума
    options.ExcludedCategories = new HashSet<string>
    {
        "Microsoft.AspNetCore.Hosting.Diagnostics",
        "Microsoft.AspNetCore.Routing.EndpointMiddleware",
        "Microsoft.AspNetCore.StaticFiles.StaticFileMiddleware"
    };
});
```

### Опции конфигурации

- `MinimumLogLevel` - минимальный уровень логирования для отправки через SignalR
- `SourceName` - название источника логов
- `ExcludedCategories` - категории логов, которые должны быть исключены
- `IncludedCategories` - категории логов, которые должны быть включены (если указаны, то только они)
- `MaxMessageLength` - максимальная длина сообщения лога
- `IncludeExceptions` - включить отправку исключений
- `IncludeStackTrace` - включить отправку stack trace

## Использование

### На сервере

Логгер автоматически перехватывает все логи, созданные через стандартный `ILogger<T>`:

```csharp
public class MyController : ControllerBase
{
    private readonly ILogger<MyController> _logger;

    public MyController(ILogger<MyController> logger)
    {
        _logger = logger;
    }

    public IActionResult Test()
    {
        _logger.LogInformation("Тестовое сообщение");
        _logger.LogError("Ошибка: {Error}", "Что-то пошло не так");
        
        return Ok();
    }
}
```

### На клиенте (JavaScript)

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/logger")
    .build();

connection.on("SendLogMessage", function (logMessage) {
    console.log("Получен лог:", logMessage);
    // Обработка лога
});

connection.start().then(function () {
    console.log("Подключено к Logger Hub");
}).catch(function (err) {
    console.error("Ошибка подключения:", err.toString());
});
```

### Структура LogMessageDto

```typescript
interface LogMessageDto {
    id: string;
    timestamp: DateTime;
    logLevel: string;
    category: string;
    message: string;
    exception?: string;
    stackTrace?: string;
    eventId?: number;
    source?: string;
    connectionId?: string;
}
```

## Тестирование

Для тестирования SignalR логгера доступны:

1. **Тестовый контроллер** - `/api/LoggerTest/`
   - `POST /api/LoggerTest/test-logging` - тестирует различные уровни логирования
   - `POST /api/LoggerTest/test-exception` - тестирует логирование исключений
   - `POST /api/LoggerTest/test-structured` - тестирует структурированное логирование

2. **Тестовая страница** - `/logger-test.html`
   - Веб-интерфейс для подключения к SignalR хабу
   - Кнопки для тестирования различных сценариев логирования
   - Отображение логов в реальном времени

## Группы клиентов

Клиенты автоматически добавляются в группу "loggers" при подключении. Это позволяет отправлять логи только определенным группам клиентов.

## История логов

Хаб поддерживает историю последних 1000 логов в памяти. История доступна через метод `GetLogHistory()`.

## Производительность

- Логи отправляются асинхронно через `Task.Run()` для избежания блокировки основного потока
- Ошибки отправки логов логируются в консоль, чтобы избежать рекурсии
- Поддерживается фильтрация по категориям и уровням логирования
- Ограничение максимальной длины сообщения

## Безопасность

- Логгер не отправляет чувствительные данные по умолчанию
- Можно настроить исключение определенных категорий логов
- Ограничение длины сообщений предотвращает отправку больших объемов данных

## Мониторинг

Логгер автоматически логирует свои собственные ошибки в консоль для мониторинга работоспособности.
