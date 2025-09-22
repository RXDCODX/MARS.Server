# Shikimori Rate Limiter

Рейт лимитер для Shikimori API с ограничениями **5 запросов в секунду** и **90 запросов в минуту**.

## Особенности

- ✅ Соблюдение лимитов API: 5rps и 90rpm
- ✅ Асинхронное ожидание доступных слотов
- ✅ Автоматическая очистка устаревших записей
- ✅ Мониторинг состояния через API
- ✅ Thread-safe реализация
- ✅ Интеграция с DI контейнером

## Архитектура

### Структура файлов

```text
Services/Shikimori/
├── Entitys/
│   ├── IRateLimiter.cs          # Интерфейс рейт лимитера
│   └── RateLimiterInfo.cs       # Модель информации о состоянии
├── ShikimoriRateLimiter.cs      # Реализация рейт лимитера
├── ShikimoriService.cs          # Основной сервис с интеграцией
└── README_RateLimiter.md        # Документация
```

### IRateLimiter

Интерфейс для управления ограничениями запросов:

- `TryAcquireAsync()` - попытка получить слот без ожидания
- `WaitForSlotAsync()` - ожидание доступности слота
- `GetInfo()` - получение информации о состоянии

### ShikimoriRateLimiter

Реализация рейт лимитера с использованием:

- `SemaphoreSlim` для ограничения одновременных запросов
- `ConcurrentQueue<DateTime>` для отслеживания запросов по времени
- Автоматическая очистка записей старше лимитного периода

## Использование

### В ShikimoriService

Все методы API автоматически используют рейт лимитер:

```csharp
public async Task<Anime?> GetRandomAnime()
{
    // Автоматически ожидает доступный слот
    await _rateLimiter.WaitForSlotAsync();
    
    // Выполняет запрос к API
    var animes = await _client.Animes.GetAnime(...);
    return animes?.FirstOrDefault();
}
```

### Мониторинг состояния

Через API endpoint `/api/ShikimoriRateLimiter/info`:

```json
{
  "availablePerSecond": 3,
  "availablePerMinute": 87,
  "timeToResetSecond": "00:00:00.500",
  "timeToResetMinute": "00:00:45.200"
}
```

## Конфигурация

Рейт лимитер настраивается через константы в `ShikimoriRateLimiter`:

```csharp
private const int MaxRequestsPerSecond = 5;    // 5rps
private const int MaxRequestsPerMinute = 90;   // 90rpm
private const int MaxConcurrentRequests = 10;  // Максимум одновременных запросов
```

## Регистрация в DI

```csharp
services.AddSingleton<IRateLimiter, ShikimoriRateLimiter>();
services.AddSingleton<ShikimoriService>();
```

## Логика работы

1. **Проверка лимитов**: Перед каждым запросом проверяются секундный и минутный лимиты
2. **Ожидание слота**: Если лимит исчерпан, запрос ожидает освобождения слота
3. **Запись запроса**: После успешного запроса время записывается в очереди
4. **Очистка**: Устаревшие записи автоматически удаляются при проверке лимитов

## Обработка ошибок

- Все исключения логируются через `ILogger`
- При ошибке слот освобождается через `SemaphoreSlim.Release()`
- Методы возвращают `null` при ошибках (согласно стилю "один вход - один выход")

## Производительность

- Минимальное потребление памяти благодаря автоматической очистке
- Эффективная работа с `ConcurrentQueue` для thread-safe операций
- Оптимизированные вычисления времени сброса лимитов
