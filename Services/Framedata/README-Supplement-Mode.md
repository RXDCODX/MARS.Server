# Режим дополнения фреймдаты

## Описание

Режим дополнения позволяет вторичному парсеру заполнять только пустые поля в данных фреймдаты, не трогая уже заполненные поля. Это полезно для объединения данных из разных источников.

## Ключевые особенности

### 1. Логика дополнения

- Заполняет только `null` поля
- Не перезаписывает существующие данные
- Для boolean полей использует логическое ИЛИ (`||`)
- Проходит всех персонажей обязательно

### 2. Обработка через StagingService

- Все изменения проходят через `StagingService`
- Фильтрует незначительные изменения (null → null)
- Использует `HasSignificantChanges` вместо простого сравнения

### 3. API эндпоинты

#### POST `/api/framedata/supplement`

Запускает дополнение с кастомными настройками:

```json
{
  "source": "Wavu", // или "Tekkendocs"
  "requestDelaySeconds": 2,
  "characterDelaySeconds": 5,
  "useStagingService": true,
  "parseMoves": true,
  "maxRetries": 3,
  "httpTimeoutSeconds": 30
}
```

#### POST `/api/framedata/supplement/{source}`

Запускает дополнение с настройками по умолчанию для указанного источника.

## Использование в коде

### Создание парсера в режиме дополнения

```csharp
var parser = FramedataParserFactory.CreateSupplementParser(
    FramedataSource.Wavu,
    logger,
    dbContextFactory,
    stagingService,
    cancellationToken
);
```

### Запуск дополнения через сервис

```csharp
await tekken8FrameData.StartSupplementFrameData(
    chat: null,
    options: null,
    source: FramedataSource.Wavu
);
```

## Настройки

### FramedataParserOptions

- `IsSupplementMode: true` - включает режим дополнения
- Все остальные настройки работают как обычно

### Логика объединения данных

#### Для персонажей (TekkenCharacter)

- Строковые поля: `existing ?? supplement`
- Массивы: `existing ?? supplement`
- Изображения: `existing ?? supplement`

#### Для мувов (Move)

- Строковые поля: `existing ?? supplement`
- Boolean поля: `existing || supplement`
- Массивы: `existing ?? supplement`

## Примеры использования

### 1. Дополнение данных Wavu данными из Tekkendocs

```csharp
// Основной парсинг из Wavu
await tekken8FrameData.StartScrupFrameData(source: FramedataSource.Wavu);

// Дополнение пропущенных данных из Tekkendocs
await tekken8FrameData.StartSupplementFrameData(source: FramedataSource.Tekkendocs);
```

### 2. Использование через API

```bash
# Запуск дополнения из Wavu с настройками по умолчанию
curl -X POST "https://api.example.com/api/framedata/supplement/Wavu"

# Запуск дополнения с кастомными настройками
curl -X POST "https://api.example.com/api/framedata/supplement" \
  -H "Content-Type: application/json" \
  -d '{
    "source": "Tekkendocs",
    "requestDelaySeconds": 1,
    "characterDelaySeconds": 3
  }'
```

## Технические детали

### StagingService изменения

- Метод `HasSignificantChanges` фильтрует изменения null → null
- Рекурсивная проверка JSON объектов
- Игнорирует незначительные изменения

### BaseFramedataParser изменения

- Методы `SupplementCharacter` и `SupplementMove`
- Проверка `Options.IsSupplementMode`
- Логика объединения данных

### Tekken8FrameData изменения

- Метод `StartSupplementFrameData`
- Метод `SupplementWithCustomOptions`
- Автоматический выбор вторичного источника
