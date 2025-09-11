# RxdcodxViewersService

Сервис для получения информации о зрителях канала Twitch `rxdcodx`.

## Описание

`RxdcodxViewersService` предоставляет методы для работы с API Twitch и получения информации о:

- Фоловерах канала
- VIP пользователях
- Модераторах канала

## Основные возможности

### Получение списков пользователей

- `GetAllFollowers()` - получить всех фоловеров (ChannelFollower)
- `GetAllFollowersInfo()` - получить всех фоловеров (FollowerInfo)
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
- `GetFollowerInfo(userId)` - получить подробную информацию о фоловере

### Кеширование фоловеров

- **Автоматическое кеширование** - кеш фоловеров загружается при запуске сервиса
- **Concurrent коллекции** - потокобезопасное хранение данных
- **Обновление через события** - кеш автоматически обновляется при получении события `ChannelFollow`
- **Fallback на кеш** - при ошибках API используются данные из кеша
- `RefreshFollowersCacheAsync()` - принудительное обновление кеша
- `GetCachedFollowersCount()` - количество фоловеров в кеше
- `ClearFollowersCache()` - очистка кеша

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

### Основные endpoints (возвращают FollowerInfo)

- `GET /api/RxdcodxViewers/all` - получить всех пользователей с полной информацией
- `GET /api/RxdcodxViewers/followers` - получить всех фоловеров (только фоловеры)
- `GET /api/RxdcodxViewers/vips` - получить всех VIP
- `GET /api/RxdcodxViewers/moderators` - получить всех модераторов
- `GET /api/RxdcodxViewers/stats` - получить статистику канала
- `GET /api/RxdcodxViewers/user/{userId}/status` - проверить статус пользователя

### Новые endpoints для работы с FollowerInfo

- `GET /api/RxdcodxViewers/followers-info` - получить всех пользователей как FollowerInfo
- `GET /api/RxdcodxViewers/user/{userId}/info` - получить полную информацию о пользователе

### Управление кешем

- `POST /api/RxdcodxViewers/refresh-cache` - принудительно обновить кеш
- `POST /api/RxdcodxViewers/clear-cache` - очистить кеш пользователей

## Требования

- Валидный токен доступа к Twitch API
- Сервис `TokenService` должен быть зарегистрирован
- Сервис `ITwitchAPI` должен быть зарегистрирован

## Кеширование фоловеров

### Автоматическое кеширование

Сервис автоматически загружает кеш фоловеров при запуске приложения. Это обеспечивает:

- **Быстрый доступ** к данным даже при недоступности API
- **Надежность** - сервис продолжает работать при сбоях Twitch API
- **Актуальность** - кеш обновляется через WebSocket события

### Обновление кеша

Кеш обновляется в следующих случаях:

1. **При запуске сервиса** - полная загрузка всех фоловеров
2. **При получении события `ChannelFollow`** - добавление нового фоловера
3. **При вызове `RefreshFollowersCacheAsync()`** - принудительное обновление
4. **При успешном API запросе** - обновление кеша актуальными данными

### Использование кеша

При ошибках API сервис автоматически переключается на использование кеша:

```csharp
// Если API недоступен, вернется кеш
var followers = await _viewersService.GetAllFollowers();

// Проверка статуса пользователя также использует кеш
var isFollower = await _viewersService.IsUserFollower(userId);
```

## Обработка ошибок

Сервис корректно обрабатывает следующие ситуации:

- Отсутствие токена доступа (возвращает кеш или `null`)
- Ошибки API Twitch (использует кеш при наличии)
- Сетевые ошибки (fallback на кеш)
- Потокобезопасность (использует `lock` для доступа к кешу)

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

### Работа с FollowerInfo

```csharp
// Получение расширенной информации о фоловерах
var followersInfo = await _viewersService.GetAllFollowersInfo();
if (followersInfo != null)
{
    foreach (var follower in followersInfo)
    {
        Console.WriteLine($"Фоловер: {follower.UserName}");
        Console.WriteLine($"Подписался: {follower.FollowedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Последнее обновление: {follower.LastUpdated:yyyy-MM-dd HH:mm:ss}");
        
        // Проверка актуальности данных
        if (follower.IsStale(TimeSpan.FromHours(1)))
        {
            Console.WriteLine("Данные устарели");
        }
    }
}

// Получение информации о конкретном фоловере
var followerInfo = await _viewersService.GetFollowerInfo("123456789");
if (followerInfo != null)
{
    Console.WriteLine($"Найден фоловер: {followerInfo}");
}
```

### Управление кешем

```csharp
// Получение количества фоловеров в кеше
var cachedCount = _viewersService.GetCachedFollowersCount();
Console.WriteLine($"В кеше {cachedCount} фоловеров");

// Очистка кеша
_viewersService.ClearFollowersCache();

// Принудительное обновление кеша
await _viewersService.RefreshFollowersCacheAsync();
```

### Примеры API запросов

#### Получение всех пользователей

```bash
GET /api/RxdcodxViewers/all
```

Ответ:

```json
{
  "count": 150,
  "users": [
    {
      "userId": "123456789",
      "userName": "username",
      "userLogin": "username",
      "followedAt": "2024-01-01T00:00:00Z",
      "isModerator": false,
      "isVip": false,
      "lastUpdated": "2024-01-15T10:30:00Z",
      "status": "Follower"
    }
  ]
}
```

#### Получение только фоловеров

```bash
GET /api/RxdcodxViewers/followers
```

#### Получение только VIP

```bash
GET /api/RxdcodxViewers/vips
```

#### Получение только модераторов

```bash
GET /api/RxdcodxViewers/moderators
```

#### Получение статистики

```bash
GET /api/RxdcodxViewers/stats
```

Ответ:

```json
{
  "followersCount": 120,
  "vipsCount": 15,
  "moderatorsCount": 5,
  "totalSpecialUsers": 20,
  "totalUsers": 140,
  "cachedUsersCount": 140
}
```

#### Проверка статуса пользователя

```bash
GET /api/RxdcodxViewers/user/123456789/status
```

#### Получение полной информации о пользователе

```bash
GET /api/RxdcodxViewers/user/123456789/info
```

#### Управление кешем

```bash
# Обновить кеш
POST /api/RxdcodxViewers/refresh-cache

# Очистить кеш
POST /api/RxdcodxViewers/clear-cache
```

## Примечания

- Все методы асинхронные
- Сервис использует пагинацию для получения больших списков
- **Concurrent коллекции** - кеш потокобезопасен и не требует блокировок
- **FollowerInfo** - расширенная модель с дополнительными возможностями
- Рекомендуется использовать `AsNoTracking()` при работе с Entity Framework для лучшей производительности
- Сервис автоматически обрабатывает ограничения API Twitch (100 записей за запрос)
- Кеш автоматически обновляется при получении WebSocket событий
