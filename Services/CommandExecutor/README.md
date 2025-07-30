# Система команд с поддержкой DI

## Обзор

Новая система команд позволяет автоматически создавать команды с внедрением зависимостей (DI) без необходимости их ручной регистрации в сервис провайдере.

## Основные компоненты

### 1. CommandFactory
Фабрика команд, которая автоматически:
- Находит все классы, наследующие от `BaseCommand`
- Создает экземпляры команд с внедрением зависимостей
- Обрабатывает ошибки создания команд

### 2. CommandExecutorService
Обновленный сервис, который:
- Использует `CommandFactory` для автоматического создания команд
- Поддерживает алиасы команд через атрибуты
- Не требует ручной регистрации команд

### 3. AliasAttribute
Атрибут для создания алиасов команд:
```csharp
[Alias("sysinfo")]
public class SystemInfoCommand : BaseCommand
```

## Создание новой команды

### Простая команда без зависимостей
```csharp
[Description("Простая команда")]
public class SimpleCommand : BaseCommand
{
    public override string CommandName => "simple";
    public override string Description => "Простая команда";
    public override bool IsAdminCommand => false;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Простая команда выполнена!");
    }
}
```

### Команда с зависимостями
```csharp
[Description("Команда с DI")]
[Alias("di")]
public class DiCommand : BaseCommand
{
    private readonly ILogger<DiCommand> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public DiCommand(
        ILogger<DiCommand> logger,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    public override string CommandName => "di";
    public override string Description => "Команда с DI";
    public override bool IsAdminCommand => false;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Выполняется команда с DI");
        
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Ваша логика здесь
        
        return "Команда с DI выполнена!";
    }
}
```

### Команда с параметрами
```csharp
[Description("Команда с параметрами")]
[CommandParameter("name", "Имя пользователя", "string", true)]
[CommandParameter("age", "Возраст", "int", false, "18")]
public class ParameterCommand : BaseCommand
{
    public override string CommandName => "parameter";
    public override string Description => "Команда с параметрами";
    public override bool IsAdminCommand => false;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        var name = parameters["name"].ToString();
        var age = (int)parameters["age"];
        
        return Task.FromResult($"Привет, {name}! Тебе {age} лет.");
    }
}
```

## Регистрация в DI

Команды автоматически регистрируются через `CommandFactory`. В `Program.cs` добавьте:

```csharp
// Регистрируем фабрику команд
services.AddSingleton<CommandFactory>();
services.AddSingleton<ICommandService, CommandExecutorService>();
```

## Преимущества новой системы

1. **Автоматическая регистрация**: Не нужно вручную регистрировать каждую команду
2. **Поддержка DI**: Легко добавлять зависимости в конструктор команд
3. **Алиасы**: Поддержка алиасов через атрибуты
4. **Обработка ошибок**: Автоматическая обработка ошибок создания команд
5. **Масштабируемость**: Легко добавлять новые команды

## Миграция существующих команд

Для миграции существующих команд:

1. Убедитесь, что команда наследует от `BaseCommand`
2. Добавьте необходимые зависимости в конструктор
3. Удалите ручную регистрацию из `CommandExecutorService`
4. Команда будет автоматически обнаружена и создана

## Примеры использования

### Команда с несколькими сервисами
```csharp
public class ComplexCommand : BaseCommand
{
    private readonly ILogger<ComplexCommand> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly TokenService _tokenService;
    private readonly EventSubService _eventSubService;

    public ComplexCommand(
        ILogger<ComplexCommand> logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        TokenService tokenService,
        EventSubService eventSubService)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _tokenService = tokenService;
        _eventSubService = eventSubService;
    }

    // Реализация команды...
}
```

### Команда с алиасами
```csharp
[Alias("fd")]
[Alias("framedata")]
public class FramedataCommand : BaseCommand
{
    // Реализация команды...
}
```

## Обработка ошибок

Если команда не может быть создана из-за отсутствующих зависимостей, система:
1. Логирует ошибку
2. Продолжает работу с остальными командами
3. Не прерывает запуск приложения

## Лучшие практики

1. **Используйте интерфейсы**: Внедряйте зависимости через интерфейсы
2. **Логирование**: Добавляйте логирование в команды
3. **Обработка исключений**: Обрабатывайте исключения в командах
4. **Алиасы**: Используйте алиасы для удобства пользователей
5. **Документация**: Добавляйте описания к командам и параметрам 