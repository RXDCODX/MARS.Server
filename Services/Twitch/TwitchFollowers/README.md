# RxdcodxViewersService

Сервис для получения информации о зрителях канала Twitch `rxdcodx`.

## Описание

`RxdcodxViewersService` предоставляет методы для работы с API Twitch и получения информации о:

- Фоловерах канала
- VIP пользователях
- Модераторах канала

## Основные возможности

### Получение списков пользователей

- `GetAllFollowers()` - получить всех фоловеров
- `GetAllViPs()` - получить всех VIP
- `GetModerators()` - получить всех модераторов

### Получение статистики

- `GetFollowersCount()` - количество фоловеров
- `GetVIPsCount()` - количество VIP
- `GetModeratorsCount()` - количество модераторов

### Проверка статуса пользователя

- `IsUserFollower(userId)` - проверить, является ли пользователь фоловером
- `IsUserVIP(userId)` - проверить, является ли пользователь VIP
- `IsUserModerator(userId)` - проверить, является ли пользователь модератором

## Установка и настройка

### 1. Регистрация в DI контейнере

```csharp
// В Program.cs или Startup.cs
using MARS.Server.Services.Twitch.TwitchFollowers;

// Добавить как Scoped сервис (рекомендуется)
services.AddRxdcodxViewersService();

// Или как Singleton сервис
services.AddRxdcodxViewersServiceAsSingleton();
```

### 2. Использование в сервисах

```csharp
public class MyService
{
    private readonly IRxdcodxViewersService _viewersService;

    public MyService(IRxdcodxViewersService viewersService)
    {
        _viewersService = viewersService;
    }

    public async Task<int> GetChannelFollowersCount()
    {
        return await _viewersService.GetFollowersCount();
    }
}
```

### 3. Использование в контроллерах

```csharp
[ApiController]
[Route("api/[controller]")]
public class MyController : ControllerBase
{
    private readonly IRxdcodxViewersService _viewersService;

    public MyController(IRxdcodxViewersService viewersService)
    {
        _viewersService = viewersService;
    }

    [HttpGet("followers")]
    public async Task<IActionResult> GetFollowers()
    {
        var followers = await _viewersService.GetAllFollowers();
        return Ok(followers);
    }
}
```

## API Endpoints

После регистрации сервиса и контроллера `RxdcodxViewersController` будут доступны следующие endpoints:

- `GET /api/RxdcodxViewers/followers` - получить всех фоловеров
- `GET /api/RxdcodxViewers/vips` - получить всех VIP
- `GET /api/RxdcodxViewers/moderators` - получить всех модераторов
- `GET /api/RxdcodxViewers/stats` - получить статистику канала
- `GET /api/RxdcodxViewers/user/{userId}/status` - проверить статус пользователя

## Требования

- Валидный токен доступа к Twitch API
- Сервис `TokenService` должен быть зарегистрирован
- Сервис `ITwitchAPI` должен быть зарегистрирован

## Обработка ошибок

Сервис корректно обрабатывает следующие ситуации:

- Отсутствие токена доступа (возвращает `null`)
- Ошибки API Twitch (выбрасывает исключения с описанием)
- Сетевые ошибки

## Примеры использования

### Получение статистики канала

```csharp
var stats = new
{
    Followers = await _viewersService.GetFollowersCount(),
    VIPs = await _viewersService.GetVIPsCount(),
    Moderators = await _viewersService.GetModeratorsCount()
};
```

### Проверка статуса пользователя

```csharp
var userId = "123456789";
var status = await _viewersService.IsUserFollower(userId);
if (status)
{
    Console.WriteLine($"Пользователь {userId} является фоловером");
}
```

### Получение всех фоловеров с обработкой ошибок

```csharp
try
{
    var followers = await _viewersService.GetAllFollowers();
    if (followers != null)
    {
        foreach (var follower in followers)
        {
            Console.WriteLine($"Фоловер: {follower.UserName} (ID: {follower.UserId})");
        }
    }
    else
    {
        Console.WriteLine("Токен недоступен");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка: {ex.Message}");
}
```

## Примечания

- Все методы асинхронные
- Сервис использует пагинацию для получения больших списков
- Рекомендуется использовать `AsNoTracking()` при работе с Entity Framework для лучшей производительности
- Сервис автоматически обрабатывает ограничения API Twitch (100 записей за запрос)
