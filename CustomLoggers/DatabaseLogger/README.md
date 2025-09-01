# Database Logger для MARS.Server

## Описание

Кастомный логгер, который записывает логи в базу данных PostgreSQL. Логирует только сообщения с уровнем выше `Information` (Warning, Error, Critical).

## Структура таблицы

Таблица `logs.Errors` содержит следующие поля:

- `Id` - уникальный идентификатор (GUID)
- `WhenLogged` - время записи лога
- `Message` - сообщение лога
- `StackTrace` - стек вызовов (если есть исключение)
- `LogLevel` - уровень логирования (Warning, Error, Critical)

## Настройка

Логгер автоматически настраивается в `Program.cs` для всех окружений:

```csharp
builder.Logging.AddDbLogger(() =>
{
    var options = new DbContextOptionsBuilder<LoggerDbContext>();
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    options.EnableThreadSafetyChecks();
    
    if (builder.Environment.IsDevelopment())
    {
        options.UseNpgsql(configuration.GetConnectionString("Dev_Path"));
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
    else
    {
        options.UseNpgsql(configuration.GetConnectionString("Prod_Path"));
    }

    return new DbLoggerOptions { DbContext = new LoggerDbContext(options.Options) };
});
```

## Использование

Логгер автоматически используется во всех контроллерах и сервисах через `ILogger<T>`:

```csharp
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;

    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
    }

    [HttpPost("test")]
    public IActionResult Test()
    {
        _logger.LogWarning("Предупреждение");
        _logger.LogError("Ошибка");
        _logger.LogCritical("Критическая ошибка");
        
        return Ok();
    }
}
```

## Тестирование

Для тестирования логгера используйте `TestLoggerController`:

- `POST /api/TestLogger/test-warning` - тест предупреждения
- `POST /api/TestLogger/test-error` - тест ошибки
- `POST /api/TestLogger/test-critical` - тест критической ошибки

## Миграции

Для создания/обновления таблицы логов используйте:

```bash
# Создать миграцию
dotnet ef migrations add MigrationName --context LoggerDbContext

# Применить миграцию
dotnet ef database update --context LoggerDbContext

# Откатить миграцию
dotnet ef database update PreviousMigrationName --context LoggerDbContext
```

## Особенности

- Логи записываются только для уровней Warning, Error и Critical
- При ошибке записи в БД, ошибка выводится в консоль
- Используется схема `logs` в базе данных
- Поддерживает PostgreSQL
