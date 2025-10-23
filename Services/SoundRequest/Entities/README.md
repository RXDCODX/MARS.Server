# SoundRequest - Новая структура базы данных

## Обзор изменений

Структура базы данных была реорганизована для нормализации данных. Теперь вместо одной таблицы `BaseTrackInfo` используются две связанные таблицы:

1. **BaseTrackInfo** - хранит информацию о треках (без дублирования)
2. **QueueItem** - хранит заказы пользователей

## Структура таблиц

### BaseTrackInfo (Треки)

Таблица для хранения уникальных треков. Один трек может быть заказан несколько раз разными пользователями.

**Основные поля:**

- `Id` (Guid) - уникальный идентификатор трека
- `TrackName` (string) - название трека
- `Authors` (string[]) - авторы трека
- `Duration` (TimeSpan) - длительность трека
- `Url` (Uri) - URL трека (**уникальный индекс**)
- `ArtworkUrl` (Uri?) - URL обложки
- `VideoId` (string?) - ID видео на платформе
- `IsDeleted` (bool) - мягкое удаление
- `CreatedAt` (DateTime) - дата создания
- `UpdatedAt` (DateTime) - дата обновления
- `LastTimePlays` (DateTime) - время последнего воспроизведения

**Связи:**

- `QueueItems` (ICollection<QueueItem>) - все заказы этого трека

**Устаревшие поля (для обратной совместимости):**

- `QueueOrder` - **deprecated**, используйте `QueueItem.QueueOrder`
- `RequestedByTwitchId` - **deprecated**, используйте `QueueItem.RequestedByTwitchId`
- `RequestedByTwitchUser` - **deprecated**, используйте `QueueItem.RequestedByTwitchUser`

### QueueItem (Элементы очереди)

Таблица для хранения заказов пользователей. Каждый заказ - это связь между треком и пользователем.

**Основные поля:**

- `Id` (Guid) - уникальный идентификатор элемента очереди
- `TrackId` (Guid) - ID трека (**foreign key**)
- `Track` (BaseTrackInfo) - ссылка на трек
- `QueueOrder` (int?) - порядок в очереди (**индекс**)
- `RequestedByTwitchId` (string) - Twitch ID пользователя (**foreign key**)
- `RequestedByTwitchUser` (TwitchUser) - ссылка на пользователя
- `RequestedAt` (DateTime) - дата и время заказа
- `IsDeleted` (bool) - мягкое удаление

**Связи:**

- `Track` (BaseTrackInfo) - трек, связанный с этим заказом
- `RequestedByTwitchUser` (TwitchUser) - пользователь, заказавший трек

### PlayerState (Состояние плеера)

Таблица для хранения текущего состояния плеера.

**Изменения:**

- `CurrentTrackId` → `CurrentQueueItemId` (Guid?)
- `NextTrackId` → `NextQueueItemId` (Guid?)
- `CurrentTrack` → `CurrentQueueItem` (QueueItem?)
- `NextTrack` → `NextQueueItem` (QueueItem?)

**Удалённые поля:**

- `CurrentTrackRequestedBy` - теперь доступно через `CurrentQueueItem.RequestedByTwitchId`
- `CurrentTrackRequestedByTwitchUser` - теперь доступно через `CurrentQueueItem.RequestedByTwitchUser`

## Преимущества новой структуры

### 1. Нормализация данных

- Треки хранятся один раз (идентифицируются по URL)
- Нет дублирования информации о треках

### 2. История заказов

- Каждый заказ трека сохраняется как отдельная запись
- Можно узнать, кто и когда заказывал конкретный трек
- История заказов не теряется при удалении из очереди

### 3. Статистика

- Легко получить статистику: какие треки самые популярные
- Можно узнать, сколько раз трек был заказан
- Можно узнать, кто чаще всего заказывает треки

### 4. Гибкость

- Один и тот же трек может быть в очереди несколько раз от разных пользователей
- Можно легко добавить новые поля к заказу (например, приоритет, комментарий и т.д.)

## Диаграмма связей

```
┌─────────────────────┐
│   TwitchUser        │
│                     │
│  - TwitchId (PK)    │
│  - DisplayName      │
│  - UserLogin        │
└──────────┬──────────┘
           │
           │ 1:N
           │
┌──────────▼──────────┐        ┌─────────────────────┐
│   QueueItem         │   N:1  │   BaseTrackInfo     │
│                     ├────────►                     │
│  - Id (PK)          │        │  - Id (PK)          │
│  - TrackId (FK)     │        │  - TrackName        │
│  - RequestedById(FK)│        │  - Authors          │
│  - QueueOrder       │        │  - Duration         │
│  - RequestedAt      │        │  - Url (UNIQUE)     │
│  - IsDeleted        │        │  - ArtworkUrl       │
└──────────┬──────────┘        │  - VideoId          │
           │                   │  - IsDeleted        │
           │                   │  - CreatedAt        │
           │ 1:1                │  - UpdatedAt        │
           │                   │  - LastTimePlays    │
┌──────────▼──────────┐        └─────────────────────┘
│   PlayerState       │
│                     │
│  - Id (PK)          │
│  - CurrentQueueId(FK)│
│  - NextQueueId (FK) │
│  - State            │
│  - Volume           │
│  - IsMuted          │
│  - CurrentProgress  │
└─────────────────────┘
```

## Миграция данных

### Шаги миграции

1. **Создать новые таблицы** (через EF миграцию)

   ```bash
   dotnet ef migrations add SoundRequest_SplitTracksAndQueue
   dotnet ef database update
   ```

2. **Перенести данные** из старой структуры:

   ```sql
   -- Переносим уникальные треки
   INSERT INTO "SoundRequestQueueItems" 
   ("TrackId", "RequestedByTwitchId", "QueueOrder", "RequestedAt", "IsDeleted")
   SELECT 
       "Id",
       "RequestedByTwitchId",
       "QueueOrder",
       NOW(),  -- или используйте подходящую дату
       "IsDeleted"
   FROM "SoundRequestBaseTrackInfos"
   WHERE "QueueOrder" IS NOT NULL;
   
   -- Обновляем PlayerState
   UPDATE "SoundRequestPlayerState"
   SET 
       "CurrentQueueItemId" = (
           SELECT "Id" FROM "SoundRequestQueueItems" 
           WHERE "TrackId" = "CurrentTrackId" 
           LIMIT 1
       ),
       "NextQueueItemId" = (
           SELECT "Id" FROM "SoundRequestQueueItems" 
           WHERE "TrackId" = "NextTrackId" 
           LIMIT 1
       );
   ```

3. **Очистить устаревшие поля** в `BaseTrackInfo`:

   ```sql
   UPDATE "SoundRequestBaseTrackInfos"
   SET 
       "QueueOrder" = NULL,
       "RequestedByTwitchId" = NULL;
   ```

## Изменения в API

### Обновленные методы

**SoundRequestController:**

- `GET /api/soundrequest/queue` - теперь возвращает `List<QueueItem>` вместо `List<BaseTrackInfo>`
- `POST /api/soundrequest/play-track/{queueItemId}` - принимает `queueItemId` вместо `trackId`
- `DELETE /api/soundrequest/queue/{queueItemId}` - принимает `queueItemId` вместо `trackId`

**SignalR Hub (ISoundRequestHub):**

- `QueueChanged(List<QueueItem>)` - отправляет `List<QueueItem>` вместо `List<BaseTrackInfo>`

### Новые методы

**MainPlayer:**

- `AddTrackAsync(track, twitchId, user)` - добавляет трек с информацией о пользователе
- `PlayQueueItemAsync(queueItemId)` - воспроизводит элемент очереди
- `RemoveQueueItemAsync(queueItemId)` - удаляет элемент из очереди

**SoundRequestUserQueue:**

- `AddToQueueAsync(track, twitchId, user)` - создает элемент очереди
- `GetNextQueueItemAsync()` - получает следующий элемент очереди
- `GetQueueItemByIdAsync(id)` - получает элемент по ID
- `GetUserQueueItemsAsync(twitchId)` - получает элементы пользователя

## Совместимость

### Устаревшие методы (deprecated)

Следующие методы помечены как `[Obsolete]` и будут удалены в будущем:

**StateManager:**

- `SetCurrentTrackAsync(track)` → используйте `SetCurrentQueueItemAsync(queueItem)`
- `SetNextTrackAsync(track)` → используйте `SetNextQueueItemAsync(queueItem)`
- `StartPlayingAsync(track, user)` → используйте `StartPlayingAsync(queueItem)`

**MainPlayer:**

- `PlayAsync(track, user)` → используйте `PlayAsync(queueItem)`

Эти методы будут работать, создавая временные `QueueItem` объекты, но рекомендуется использовать новые методы.

## Обратная совместимость

### Entity BaseTrackInfo

Устаревшие поля помечены как `[Obsolete]` но сохранены в entity для обратной совместимости:

- `QueueOrder`
- `RequestedByTwitchId`
- `RequestedByTwitchUser`
- `LastTimePlays`

Эти поля будут удалены после полной миграции всех данных и обновления клиентского кода.

### Переходный период

Во время переходного периода:

1. Старые поля сохраняются в базе данных
2. Новая логика использует `QueueItem`
3. Устаревшие методы создают временные объекты для совместимости
4. После миграции данных можно удалить устаревшие поля

## Следующие шаги

1. ✅ Создать entity классы `QueueItem` и обновить `BaseTrackInfo`
2. ✅ Обновить `AppDbContext` для новых таблиц
3. ⏳ Создать миграцию EF (выполнит пользователь)
4. ⏳ Перенести данные из старой структуры
5. ⏳ Обновить клиентский код для работы с новым API
6. ⏳ Регенерировать Swagger API
7. ⏳ Удалить устаревшие поля и методы

## Примеры использования

### Добавление трека в очередь

**Старый способ:**

```csharp
var track = new BaseTrackInfo { ... };
track.RequestedByTwitchId = user.TwitchId;
await queue.AddToQueueAsync(track);
```

**Новый способ:**

```csharp
var track = new BaseTrackInfo { ... };
var queueItem = await queue.AddToQueueAsync(track, user.TwitchId, user);
```

### Воспроизведение трека

**Старый способ:**

```csharp
var track = await queue.GetNextTrackAsync();
await player.PlayAsync(track, track.RequestedByTwitchUser, ct);
```

**Новый способ:**

```csharp
var queueItem = await queue.GetNextQueueItemAsync();
await player.PlayAsync(queueItem, ct);
```

### Получение очереди

**Старый способ:**

```csharp
List<BaseTrackInfo> queue = await manager.GetQueueAsync();
foreach (var track in queue)
{
    Console.WriteLine($"{track.TrackName} - {track.RequestedByTwitchUser.DisplayName}");
}
```

**Новый способ:**

```csharp
List<QueueItem> queue = await player.GetQueueAsync();
foreach (var queueItem in queue)
{
    Console.WriteLine($"{queueItem.Track.TrackName} - {queueItem.RequestedByTwitchUser.DisplayName}");
}
```

## Заметки

- Все изменения обратно совместимы благодаря устаревшим методам
- Миграцию данных можно выполнить без остановки сервиса (зависит от объема данных)
- Рекомендуется обновить клиентский код как можно скорее для использования новой структуры
