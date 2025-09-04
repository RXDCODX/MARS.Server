# Logs Service

Сервис для работы с логами MARS.Server предоставляет REST API для получения, фильтрации и сортировки логов.

## API Endpoints

### Получение логов с пагинацией и фильтрами

```
GET /api/logs
```

**Параметры запроса:**

- `page` (int, optional): Номер страницы (по умолчанию 1)
- `pageSize` (int, optional): Размер страницы (по умолчанию 50, максимум 1000)
- `sortBy` (string, optional): Поле для сортировки:
  - `whenlogged` - по дате логирования
  - `loglevel` - по уровню логирования
  - `message` - по сообщению
- `sortDescending` (bool, optional): Сортировка по убыванию (по умолчанию true)
- `logLevel` (string, optional): Фильтр по уровню логирования (Warning, Error, Critical)
- `fromDate` (DateTimeOffset, optional): Дата начала периода
- `toDate` (DateTimeOffset, optional): Дата окончания периода
- `searchText` (string, optional): Поисковый текст в сообщении или стек-трейсе

**Пример:**

```
GET /api/logs?page=1&pageSize=20&sortBy=whenlogged&sortDescending=true&logLevel=Error&fromDate=2025-01-01T00:00:00Z
```

### Получение логов по уровню

```
GET /api/logs/by-level/{logLevel}
```

**Пример:**

```
GET /api/logs/by-level/Error
```

### Получение логов за период

```
GET /api/logs/by-date-range?fromDate={fromDate}&toDate={toDate}
```

**Пример:**

```
GET /api/logs/by-date-range?fromDate=2025-01-01T00:00:00Z&toDate=2025-01-31T23:59:59Z
```

### Получение последних логов

```
GET /api/logs/recent?count={count}
```

**Пример:**

```
GET /api/logs/recent?count=100
```

### Получение статистики по логам

```
GET /api/logs/statistics
```

**Ответ:**

```json
{
  "totalLogs": 1500,
  "warningLogs": 500,
  "errorLogs": 300,
  "criticalLogs": 50,
  "oldestLogDate": "2025-01-01T00:00:00Z",
  "newestLogDate": "2025-01-31T23:59:59Z"
}
```

## Структура ответа

### LogResponse (для GET /api/logs)

```json
{
  "logs": [
    {
      "id": "guid",
      "whenLogged": "2025-01-31T12:00:00Z",
      "message": "Error message",
      "stackTrace": "Stack trace...",
      "logLevel": "Error"
    }
  ],
  "totalCount": 1500,
  "page": 1,
  "pageSize": 50,
  "totalPages": 30
}
```

## Использование

Сервис автоматически регистрируется в DI контейнере и доступен через интерфейс `ILogsService`.

```csharp
public class SomeController : ControllerBase
{
    private readonly ILogsService _logsService;

    public SomeController(ILogsService logsService)
    {
        _logsService = logsService;
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs()
    {
        var (logs, totalCount) = await _logsService.GetLogsAsync();
        return Ok(new { logs, totalCount });
    }
}
```

## Особенности

- Все запросы к базе данных используют `AsNoTracking()` для оптимизации производительности
- Максимальный размер страницы ограничен 1000 записями
- API поддерживает фильтрацию по дате, уровню логирования и поисковому тексту
- Сортировка доступна по дате, уровню и сообщению
- Сервис работает с PostgreSQL базой данных в схеме `logs`
