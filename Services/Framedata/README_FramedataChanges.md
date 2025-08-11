# Система отслеживания изменений в фреймдате Tekken 8

## Обзор

Система автоматически отслеживает изменения в фреймдате Tekken 8 на сайте wavu.wiki и предоставляет возможность применять или отклонять обнаруженные изменения.

## Компоненты

### 1. Сущности базы данных

#### FramedataChange

Основная сущность для отслеживания изменений:

- `Id` - уникальный идентификатор
- `CharacterName` - имя персонажа
- `ChangeType` - тип изменения (NewCharacter, NewMove, MoveUpdate, MoveRemoval, CharacterUpdate)
- `DetectedAt` - время обнаружения
- `AppliedAt` - время применения (null если не применено)
- `Status` - статус (Pending, Applied, Rejected, Obsolete)
- `Description` - описание изменения
- `ChangeInfo` - ссылка на новую информацию
- `CurrentInfo` - ссылка на актуальную информацию

#### FramedataChangeInfo

Хранит детальную информацию об изменениях:

- `Id` - уникальный идентификатор
- `FramedataChangeId` - ссылка на изменение
- `InfoType` - тип информации (Character, Move, Movelist)
- `JsonData` - JSON данные (сериализованная информация)
- `SourceUrl` - URL источника данных
- `RetrievedAt` - время получения данных
- `DataHash` - хеш данных для сравнения

### 2. Сервисы

#### FramedataChangeDetectionService

Основной сервис для обнаружения и управления изменениями:

**Методы:**

- `StartScrupFrameData()` - запускает процесс обнаружения изменений
- `GetPendingChanges()` - получает список ожидающих изменений
- `ApplyChange(int changeId)` - применяет изменение
- `RejectChange(int changeId)` - отклоняет изменение

### 3. API контроллер

#### FramedataChangesController

REST API для управления изменениями:

**Эндпоинты:**

- `GET /api/framedatachanges/pending` - получить ожидающие изменения
- `POST /api/framedatachanges/apply/{changeId}` - применить изменение
- `POST /api/framedatachanges/reject/{changeId}` - отклонить изменение
- `POST /api/framedatachanges/detect` - запустить обнаружение изменений
- `GET /api/framedatachanges/stats` - получить статистику

## Как это работает

### 1. Обнаружение изменений

1. Система парсит главную страницу wavu.wiki
2. Для каждого персонажа извлекается информация со страницы
3. Сравнивается с данными в базе данных
4. Если обнаружены различия, создается запись об изменении

### 2. Типы изменений

- **NewCharacter** - новый персонаж
- **NewMove** - новый ход
- **MoveUpdate** - обновление существующего хода
- **MoveRemoval** - удаление хода
- **CharacterUpdate** - обновление информации о персонаже

### 3. Применение изменений

- Система автоматически применяет изменения к базе данных
- Обновляет соответствующие таблицы (TekkenCharacters, TekkenMoves)
- Отслеживает статус применения

## Использование

### Через API

```bash
# Получить ожидающие изменения
curl -X GET http://localhost:5000/api/framedatachanges/pending

# Применить изменение
curl -X POST http://localhost:5000/api/framedatachanges/apply/1

# Отклонить изменение
curl -X POST http://localhost:5000/api/framedatachanges/reject/1

# Запустить обнаружение
curl -X POST http://localhost:5000/api/framedatachanges/detect

# Получить статистику
curl -X GET http://localhost:5000/api/framedatachanges/stats
```

### Через код

```csharp
// Получить сервис
var changeDetectionService = serviceProvider.GetRequiredService<FramedataChangeDetectionService>();

// Запустить обнаружение
await changeDetectionService.StartScrupFrameData();

// Получить ожидающие изменения
var pendingChanges = await changeDetectionService.GetPendingChanges();

// Применить изменение
await changeDetectionService.ApplyChange(changeId);

// Отклонить изменение
await changeDetectionService.RejectChange(changeId);
```

## Конфигурация

### Регистрация сервисов

Сервисы автоматически регистрируются в `StartupEstensions.cs`:

```csharp
services.AddSingleton<FramedataChangeDetectionService>();
```

### База данных

Новые таблицы создаются через миграции Entity Framework:

```bash
dotnet ef migrations add AddFramedataChangeTracking
dotnet ef database update
```

## Логирование

Система использует стандартное логирование .NET:

- Информационные сообщения о процессе обнаружения
- Предупреждения о проблемах с парсингом
- Ошибки при применении изменений

## Безопасность

- Все операции с базой данных выполняются в транзакциях
- Проверка существования изменений перед применением
- Валидация данных перед сохранением
- Логирование всех операций

## Расширение

Для добавления новых типов изменений:

1. Добавить новый тип в `FramedataChangeType` enum
2. Реализовать логику обнаружения в `DetectCharacterChanges`
3. Добавить метод применения в `ApplyChange`
4. Обновить документацию

## Мониторинг

Система предоставляет статистику через API:

- Общее количество ожидающих изменений
- Распределение по типам изменений
- Распределение по персонажам
