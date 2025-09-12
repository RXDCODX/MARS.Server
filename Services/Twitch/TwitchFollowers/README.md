# RxdcodxViewersService

Сервис для получения информации о зрителях канала Twitch `rxdcodx`.

## Описание

`RxdcodxViewersService` предоставляет методы для работы с API Twitch и получения информации о:

- Фоловерах канала
- VIP пользователях
- Модераторах канала

## Основные возможности

### Получение списков пользователей

- `GetAllFollowersInfo()` - получить всех фоловеров (FollowerInfo)
- `GetUsersWithoutAvatarsAsync()` - получить пользователей без аватарок
- `GetUsersWithoutAvatarsCountAsync()` - получить количество пользователей без аватарок
- `UpdateMissingAvatarsAsync()` - обновить аватарки для пользователей без них

### Кеширование фоловеров

- **Database-First кеширование** - база данных используется как основное хранилище кеша
- **Персистентность данных** - данные сохраняются между перезапусками приложения
- **Обновление через события** - кеш автоматически обновляется при получении WebSocket событий
- **Fallback на БД** - при ошибках API используются данные из базы данных
- `RefreshFollowersCacheAsync()` - принудительное обновление кеша
- `ClearFollowersCache()` - очистка БД (асинхронный)

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

    public async Task<List<FollowerInfo>> GetUsers()
    {
        return await _viewersService.GetAllFollowersInfo() ?? new List<FollowerInfo>();
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

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var users = await _viewersService.GetAllFollowersInfo();
        return Ok(users);
    }
}
```

## API Endpoints

После регистрации сервиса и контроллера `RxdcodxViewersController` будут доступны следующие endpoints:

### Основные endpoints

- `GET /api/RxdcodxViewers/all` - получить всех пользователей с полной информацией
- `GET /api/RxdcodxViewers/without-avatars` - получить пользователей без аватарок
- `GET /api/RxdcodxViewers/without-avatars/count` - получить количество пользователей без аватарок
- `POST /api/RxdcodxViewers/update-avatars` - обновить аватарки для пользователей без них

## Требования

- Валидный токен доступа к Twitch API
- Сервис `TokenService` должен быть зарегистрирован
- Сервис `ITwitchAPI` должен быть зарегистрирован

### Database-First кеширование

Сервис использует базу данных как основное хранилище кеша. Это обеспечивает:

- **Персистентность** - данные сохраняются между перезапусками приложения
- **Надежность** - сервис продолжает работать при сбоях Twitch API
- **Актуальность** - кеш обновляется через WebSocket события
- **Простота** - нет необходимости в синхронизации между кешем и БД

### Обновление кеша

Кеш обновляется в следующих случаях:

1. **При запуске сервиса** - загрузка данных из БД или полная загрузка из API
2. **При получении WebSocket событий** - автоматическое обновление БД
3. **При вызове `RefreshFollowersCacheAsync()`** - принудительное обновление
4. **При успешном API запросе** - обновление БД актуальными данными

### Использование кеша

При ошибках API сервис автоматически переключается на использование данных из БД:

```csharp
// Если API недоступен, вернутся данные из БД
var followers = await _viewersService.GetAllFollowers();

```

## Обработка ошибок

Сервис корректно обрабатывает следующие ситуации:

- Отсутствие токена доступа (возвращает кеш или `null`)
- Ошибки API Twitch (использует кеш при наличии)
- Сетевые ошибки (fallback на кеш)
- Потокобезопасность (использует `lock` для доступа к кешу)

## Примеры использования

### Получение всех пользователей с обработкой ошибок

```csharp
try
{
    var users = await _viewersService.GetAllFollowersInfo();
    if (users != null)
    {
        foreach (var user in users)
        {
            Console.WriteLine($"Пользователь: {user.UserName} (ID: {user.UserId})");
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

```

### Управление кешем

```csharp

// Очистка БД (теперь асинхронный)
await _viewersService.ClearFollowersCache();

// Принудительное обновление кеша
await _viewersService.RefreshFollowersCacheAsync();
```

### Работа с аватарками

```csharp
// Получение пользователей без аватарок
var usersWithoutAvatars = await _viewersService.GetUsersWithoutAvatarsAsync();
Console.WriteLine($"Пользователей без аватарок: {usersWithoutAvatars.Count}");

// Получение количества пользователей без аватарок
var count = await _viewersService.GetUsersWithoutAvatarsCountAsync();
Console.WriteLine($"Количество пользователей без аватарок: {count}");

// Обновление аватарок для пользователей без них
var updatedCount = await _viewersService.UpdateMissingAvatarsAsync();
Console.WriteLine($"Обновлено аватарок: {updatedCount}");
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

#### Получение пользователей без аватарок

```bash
GET /api/RxdcodxViewers/without-avatars
```

Ответ:

```json
[
  {
    "userId": "123456789",
    "userName": "username",
    "userLogin": "username",
    "profileImageUrl": null,
    "followedAt": "2024-01-01T00:00:00Z",
    "isModerator": false,
    "isVip": false,
    "lastUpdated": "2024-01-15T10:30:00Z"
  }
]
```

#### Получение количества пользователей без аватарок

```bash
GET /api/RxdcodxViewers/without-avatars/count
```

Ответ:

```json
{
  "count": 25
}
```

#### Обновление аватарок

```bash
POST /api/RxdcodxViewers/update-avatars
```

Ответ:

```json
{
  "message": "Обновлено 15 аватарок",
  "updatedCount": 15
}
```

## Примечания

- Все методы асинхронные
- Сервис использует пагинацию для получения больших списков
- **Database-First архитектура** - БД используется как основное хранилище кеша
- **FollowerInfo** - расширенная модель с дополнительными возможностями
- Рекомендуется использовать `AsNoTracking()` при работе с Entity Framework для лучшей производительности
- Сервис автоматически обрабатывает ограничения API Twitch (100 записей за запрос)
- Кеш автоматически обновляется при получении WebSocket событий
- Данные сохраняются между перезапусками приложения
