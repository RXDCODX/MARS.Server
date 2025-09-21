# Service Manager

Система управления сервисами для MARS.Server с поддержкой персистентного состояния и централизованного управления.

## Архитектура

### ManagedServiceBase

Базовый абстрактный класс для всех управляемых сервисов, который предоставляет:

- **Автоматическую проверку `IsServiceActive`** - сервис не запустится, если отключен
- **Управление состоянием** - автоматическое обновление статуса и временных меток
- **Обработку ошибок** - централизованная обработка исключений
- **Логирование** - автоматическое логирование всех операций
- **Абстрактные методы** - обязательная реализация логики запуска/остановки

### ServiceManager

Центральный менеджер, который:

- Регистрирует все сервисы при инициализации
- Управляет состоянием в базе данных
- Предоставляет API для управления сервисами
- Поддерживает как управляемые, так и обычные hosted сервисы

## Использование

### Создание управляемого сервиса

```csharp
[ServiceName("my-service")]
public class MyService(ILogger<MyService> logger) : ManagedServiceBase(logger)
{
    public override string ServiceName => "my-service";
    public override string DisplayName => "My Service";
    public override string Description => "Описание сервиса";
    public override bool IsServiceActive { get; set; } = true;

    protected override async Task<bool> OnStartAsync(CancellationToken cancellationToken = default)
    {
        // Логика запуска сервиса
        // Возвращаем true при успехе, false при ошибке
        return true;
    }

    protected override async Task<bool> OnStopAsync(CancellationToken cancellationToken = default)
    {
        // Логика остановки сервиса
        // Возвращаем true при успехе, false при ошибке
        return true;
    }
}
```

### Регистрация в DI контейнере

```csharp
// В Program.cs или Startup.cs
builder.Services.AddHostedService<MyService>();
builder.Services.AddScoped<IServiceManager, ServiceManager>();
```

### Использование ServiceManager

```csharp
public class SomeController(IServiceManager serviceManager)
{
    public async Task<IActionResult> StartService(string serviceName)
    {
        var success = await serviceManager.StartServiceAsync(serviceName);
        return Ok(new { success });
    }
    
    public async Task<IActionResult> GetServices()
    {
        var services = await serviceManager.GetAllServicesAsync();
        return Ok(services);
    }
}
```

## Ключевые особенности

### 1. Проверка активности

Сервис автоматически проверяет `IsServiceActive` перед запуском:

- Если `IsServiceActive = false`, сервис не запустится
- Логируется предупреждение о попытке запуска отключенного сервиса

### 2. Управление состоянием

- Состояние сервисов сохраняется в таблице `ServiceStates`
- При перезапуске приложения состояние восстанавливается
- Автоматическое обновление времени последней активности

### 3. Обработка ошибок

- Все операции обернуты в try-catch
- Детальное логирование ошибок
- Graceful degradation при сбоях

### 4. Абстрактные методы

Наследники **обязаны** реализовать:

- `OnStartAsync()` - логика запуска
- `OnStopAsync()` - логика остановки

## Статусы сервисов

- `Running` - сервис работает
- `Stopped` - сервис остановлен
- `Starting` - сервис запускается
- `Stopping` - сервис останавливается
- `Error` - ошибка в работе сервиса
- `Unknown` - неизвестный статус

## Лучшие практики

1. **Всегда проверяйте результат** абстрактных методов
2. **Используйте `UpdateActivity()`** для обновления времени активности
3. **Обрабатывайте `CancellationToken`** в длительных операциях
4. **Логируйте важные события** в наследниках
5. **Освобождайте ресурсы** в `OnStopAsync()`

## Примеры

См. `Examples/ExampleManagedService.cs` для полного примера реализации.
