# RandomMeme CRUD Service

CRUD сервис для управления типами мемов и очередью мемов в системе MARS.

## Описание

Сервис предоставляет полный набор операций для работы с:

- **MemeType** - типы мемов (например, "Random Meme", "Random Sound")
- **MemeOrder** - очередь мемов с порядком воспроизведения

## Архитектура

### SOLID принципы

Сервис реализован с соблюдением принципов SOLID:

- **Single Responsibility**: Каждый метод отвечает за одну конкретную операцию
- **Open/Closed**: Легко расширяется новыми методами без изменения существующего кода
- **Liskov Substitution**: Интерфейс `IRandomMemeService` может быть заменен любой реализацией
- **Interface Segregation**: Интерфейс содержит только необходимые методы
- **Dependency Inversion**: Зависит от абстракций (`IDbContextFactory<AppDbContext>`)

### Производительность

- Используется `AsNoTracking()` для всех операций чтения
- `IDbContextFactory` для правильного управления жизненным циклом контекста
- Асинхронные операции с поддержкой `CancellationToken`

## API Endpoints

### MemeType Operations

#### GET /api/randommeme/types

Получить все типы мемов

**Response:**

```json
[
  {
    "id": 2,
    "name": "Random Meme",
    "folderPath": "Alerts\\random_meme"
  },
  {
    "id": 3,
    "name": "Random Sound",
    "folderPath": "Alerts\\zvik"
  }
]
```

#### GET /api/randommeme/types/{id}

Получить тип мема по ID

#### POST /api/randommeme/types

Создать новый тип мема

**Request Body:**

```json
{
  "name": "New Meme Type",
  "folderPath": "Alerts\\new_type"
}
```

#### PUT /api/randommeme/types/{id}

Обновить существующий тип мема

#### DELETE /api/randommeme/types/{id}

Удалить тип мема (только если нет связанных мемов)

### MemeOrder Operations

#### GET /api/randommeme/orders

Получить все мемы в очереди

**Response:**

```json
[
  {
    "id": "uuid-here",
    "order": 1,
    "filePath": "path/to/meme.jpg",
    "memeTypeId": 2,
    "type": {
      "id": 2,
      "name": "Random Meme",
      "folderPath": "Alerts\\random_meme"
    }
  }
]
```

#### GET /api/randommeme/orders/type/{typeId}

Получить мемы определенного типа

#### GET /api/randommeme/orders/{id}

Получить мем по ID

#### POST /api/randommeme/orders

Добавить новый мем в очередь

**Request Body:**

```json
{
  "filePath": "path/to/new/meme.jpg",
  "memeTypeId": 2
}
```

#### PUT /api/randommeme/orders/{id}

Обновить существующий мем

#### DELETE /api/randommeme/orders/{id}

Удалить мем из очереди

### Additional Operations

#### GET /api/randommeme/random?typeId={typeId}

Получить случайный мем (опционально фильтровать по типу)

#### GET /api/randommeme/count?typeId={typeId}

Получить количество мемов (опционально фильтровать по типу)

#### POST /api/randommeme/orders/reorder/{typeId}

Пересчитать порядок мемов для определенного типа

## Использование

### Регистрация в DI

```csharp
// В Program.cs или StartupEstensions.cs
services.AddScoped<IRandomMemeService, RandomMemeService>();
```

### Внедрение в контроллер

```csharp
public class MyController : ControllerBase
{
    private readonly IRandomMemeService _randomMemeService;

    public MyController(IRandomMemeService randomMemeService)
    {
        _randomMemeService = randomMemeService;
    }

    public async Task<IActionResult> GetRandomMeme()
    {
        var meme = await _randomMemeService.GetRandomMemeAsync();
        return Ok(meme);
    }
}
```

### Внедрение в сервис

```csharp
public class MyService
{
    private readonly IRandomMemeService _randomMemeService;

    public MyService(IRandomMemeService randomMemeService)
    {
        _randomMemeService = randomMemeService;
    }

    public async Task ProcessMeme()
    {
        var count = await _randomMemeService.GetMemeOrderCountAsync();
        // Логика обработки
    }
}
```

## Безопасность

- Валидация входных данных через Data Annotations
- Проверка существования сущностей перед обновлением/удалением
- Защита от удаления типов мемов с связанными мемами
- Логирование всех операций

## Обработка ошибок

- HTTP 400 для некорректных данных
- HTTP 404 для несуществующих ресурсов
- HTTP 500 для внутренних ошибок сервера
- Детальное логирование всех исключений

## Производительность

- Использование `AsNoTracking()` для операций чтения
- Правильное управление жизненным циклом DbContext
- Асинхронные операции для неблокирующего выполнения
- Поддержка отмены операций через CancellationToken
