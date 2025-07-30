# Система команд сервисов MARS.Server

## Обзор

Система команд сервисов позволяет автоматически связывать команды с сервисами, наследующими от `ManagedServiceBase`. Это позволяет сервисам получать список своих команд из общего реестра команд на основе атрибутов.

## Архитектура

### Основные компоненты

1. **ServiceCommandAttribute** - Атрибут для связывания команд с сервисами
2. **ServiceCommandManager** - Менеджер для управления командами сервисов
3. **ManagedServiceBase** - Базовый класс сервисов с поддержкой команд
4. **ServiceManager** - Интеграция с системой управления сервисами

### Модели данных

- **ServiceCommandInfo** - Информация о команде сервиса
- **ServiceCommandAttribute** - Атрибут для связывания команд

## Использование

### 1. Добавление атрибута к команде

```csharp
[ServiceCommand("service-name", Platform.Telegram | Platform.Api, true)]
public class MyCommand : BaseCommand
{
    // Реализация команды
}
```

**Параметры атрибута:**
- `serviceName` - Название сервиса (обязательно)
- `platforms` - Платформы, на которых команда доступна для сервиса (по умолчанию Platform.None)
- `isAdminCommand` - Является ли команда админской для сервиса (по умолчанию false)

### 2. Получение команд в сервисе

```csharp
public class MyManagedService : ManagedServiceBase
{
    public override string ServiceName => "my-service";
    
    // Получение всех команд сервиса
    public List<ServiceCommandInfo> GetAllCommands(ServiceCommandManager manager)
    {
        return GetAvailableCommandsFromManager(manager, true);
    }
    
    // Получение только админских команд
    public List<ServiceCommandInfo> GetAdminCommands(ServiceCommandManager manager)
    {
        return GetAdminCommandsFromManager(manager);
    }
}
```

### 3. Использование через ServiceManager

```csharp
// Получение команд сервиса
var commands = serviceManager.GetServiceCommands("my-service", true);

// Получение админских команд
var adminCommands = serviceManager.GetAdminCommands("my-service");

// Получение команд для определенной платформы
var platformCommands = serviceManager.GetServiceCommandsForPlatform("my-service", Platform.Telegram, true);
```

## Примеры атрибутов

### Системные команды
```csharp
[ServiceCommand("system", Platform.Telegram | Platform.Api, true)]
public class SystemInfoCommand : BaseCommand
```

### TTS команды
```csharp
[ServiceCommand("syntheziaqueue", Platform.Telegram | Platform.Api | Platform.Twitch, true)]
public class TTSVolumeCommand : BaseCommand
```

### Игровые команды
```csharp
[ServiceCommand("waifu-roll", Platform.Telegram | Platform.Api | Platform.Twitch, true)]
public class RollWaifuCommand : BaseCommand
```

### Twitch команды
```csharp
[ServiceCommand("fumofriday", Platform.Api | Platform.Telegram | Platform.Twitch, true)]
public class FumoCommand : BaseCommand
```

## Преимущества

1. **Автоматическое связывание** - Команды автоматически связываются с сервисами
2. **Фильтрация по платформам** - Можно указать, на каких платформах команда доступна для сервиса
3. **Админские права** - Можно указать, является ли команда админской для конкретного сервиса
4. **Централизованное управление** - Все команды управляются в одном месте
5. **Гибкость** - Одна команда может быть связана с несколькими сервисами

## Регистрация в DI

Все необходимые сервисы регистрируются в DI контейнере:

```csharp
// В StartupEstensions.cs
services.AddSingleton<IPlatformCommandService, TelegramCommandService>();
services.AddSingleton<IPlatformCommandService, TwitchCommandService>();
services.AddSingleton<PlatformCommandManager>();
services.AddSingleton<CommandFactory>();
services.AddScoped<ICommandService, CommandExecutorService>();

// В Program.cs
services.AddSingleton<ServiceCommandManager>();
```

### Важные моменты:
- `PlatformCommandManager` должен быть зарегистрирован до `CommandExecutorService`
- `IPlatformCommandService` реализации должны быть зарегистрированы как Singleton
- `ICommandService` регистрируется как Scoped в Telegram сервисах

## Инициализация

Команды сервисов автоматически инициализируются при запуске ServiceManager:

```csharp
private void InitializeServiceCommands()
{
    var commandFactory = _serviceProvider.GetService<CommandFactory>();
    if (commandFactory != null)
    {
        var allCommands = commandFactory.CreateAllCommands().Values;
        _serviceCommandManager.InitializeServiceCommands(allCommands);
    }
}
```

## API методы

### ServiceManager
- `GetServiceCommands(serviceName, includeAdminCommands)` - Получить команды сервиса
- `GetAdminCommands(serviceName)` - Получить админские команды сервиса
- `GetServiceCommandsForPlatform(serviceName, platform, includeAdminCommands)` - Получить команды для платформы

### ManagedServiceBase
- `GetAvailableCommandsFromManager(manager, includeAdminCommands)` - Получить команды из менеджера
- `GetAdminCommandsFromManager(manager)` - Получить админские команды из менеджера

## Безопасность

- Админские команды требуют специальных привилегий
- Фильтрация по платформам обеспечивает безопасность
- Команды проверяются на доступность перед выполнением 