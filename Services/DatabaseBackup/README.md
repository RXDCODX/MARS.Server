# Сервис резервного копирования базы данных (MemoryStorage)

Этот сервис предоставляет функциональность для создания резервных копий PostgreSQL баз данных с использованием MemoryStorage для хранения файлов в памяти.

## Возможности

- Создание резервных копий баз данных `dev` и `prod`
- Хранение файлов в MemoryStorage (в памяти)
- Скачивание файлов резервных копий через API
- Прямой доступ к файлам через `/memory/{filename}`
- Просмотр списка доступных резервных копий
- Автоматическая очистка старых резервных копий
- Мониторинг статуса резервного копирования

## Требования

- PostgreSQL установлен на сервере
- `pg_dump` доступен в системе
- Права доступа к базам данных для создания резервных копий
- MemoryStorage сервис настроен и работает

## Установка

1. Добавьте сервис в `Program.cs`:

```csharp
using MARS.Server.Services.DatabaseBackup;

// В методе ConfigureServices
services.AddDatabaseBackupService();
```

2. Убедитесь, что в `appsettings.json` настроены строки подключения:

```json
{
  "ConnectionStrings": {
    "Dev_Path": "User ID=postgres;Password=postgres;Host=localhost;Port=5432;Database=dev;",
    "Prod_Path": "User ID=postgres;Password=postgres;Host=localhost;Port=5432;Database=prod;"
  }
}
```

## API Endpoints

### Создание резервной копии

```http
POST /api/DatabaseBackup/create?databaseName=dev
```

**Параметры:**

- `databaseName` - имя базы данных (`dev` или `prod`)

**Ответ:**

```json
{
  "success": true,
  "message": "Резервная копия базы данных dev создана успешно",
  "downloadUrl": "/memory/backup_dev_20241201_143022.sql",
  "fileName": "backup_dev_20241201_143022.sql",
  "createdAt": "2024-12-01T14:30:22.123Z"
}
```

### Скачивание резервной копии через API

```http
GET /api/DatabaseBackup/download?fileName=backup_dev_20241201_143022.sql
```

**Параметры:**

- `fileName` - имя файла резервной копии

**Ответ:** Файл SQL для скачивания

### Прямое скачивание через MemoryStorage

```http
GET /memory/backup_dev_20241201_143022.sql
```

**Ответ:** Файл SQL для скачивания (автоматически удаляется после скачивания)

### Список резервных копий

```http
GET /api/DatabaseBackup/list
```

**Ответ:**

```json
{
  "success": true,
  "backups": [
    {
      "fileName": "backup_dev_20241201_143022.sql",
      "databaseName": "dev",
      "size": 1048576,
      "sizeMB": 1.0,
      "created": "2024-12-01T14:30:22.123Z",
      "contentType": "application/sql",
      "downloadUrl": "/memory/backup_dev_20241201_143022.sql"
    }
  ],
  "totalCount": 1
}
```

### Очистка старых резервных копий

```http
POST /api/DatabaseBackup/cleanup?keepCount=5
```

**Параметры:**

- `keepCount` - количество копий для сохранения (по умолчанию 10)

**Ответ:**

```json
{
  "success": true,
  "message": "Очистка завершена. Удалено 3 старых резервных копии",
  "deletedCount": 3,
  "keepCount": 5
}
```

### Статус резервного копирования

```http
GET /api/DatabaseBackup/status
```

**Ответ:**

```json
{
  "success": true,
  "status": {
    "totalBackups": 5,
    "totalSizeBytes": 5242880,
    "totalSizeMB": 5.0,
    "oldestBackup": "2024-11-01T10:00:00.000Z",
    "newestBackup": "2024-12-01T14:30:22.123Z",
    "storageInfo": "MemoryStorage"
  }
}
```

## Конфигурация

### MemoryStorage

Файлы резервных копий автоматически сохраняются в MemoryStorage и доступны по URL `/memory/{filename}`. После скачивания файл автоматически удаляется из памяти.

### Автоматическая очистка

Рекомендуется настроить автоматическую очистку старых резервных копий через планировщик задач или Hangfire:

```csharp
// Пример использования в Hangfire
RecurringJob.AddOrUpdate<IDatabaseBackupService>(
    "cleanup-backups",
    service => service.CleanupOldBackupsAsync(10),
    Cron.Daily);
```

## Безопасность

- Все операции логируются для аудита
- Файлы автоматически удаляются после скачивания
- Доступ к файлам контролируется через MemoryStorage

## Логирование

Сервис использует стандартную систему логирования .NET. Все операции записываются в лог с соответствующими уровнями:

- `Information` - успешные операции
- `Warning` - предупреждения (например, не удалось удалить временный файл)
- `Error` - ошибки при выполнении операций

## Обработка ошибок

Сервис корректно обрабатывает различные типы ошибок:

- `ArgumentException` - некорректные параметры
- `FileNotFoundException` - файл не найден в MemoryStorage
- `InvalidOperationException` - ошибки выполнения pg_dump

## Примеры использования

### Создание резервной копии через код

```csharp
public class SomeService
{
    private readonly IDatabaseBackupService _backupService;

    public SomeService(IDatabaseBackupService backupService)
    {
        _backupService = backupService;
    }

    public async Task<string> CreateDevBackupAsync()
    {
        try
        {
            var downloadUrl = await _backupService.CreateBackupAsync("dev");
            return downloadUrl;
        }
        catch (Exception ex)
        {
            // Обработка ошибок
            throw;
        }
    }
}
```

### Получение списка резервных копий

```csharp
public async Task<List<string>> GetRecentBackupsAsync()
{
    var backups = await _backupService.GetAvailableBackupsAsync();
    return backups.Take(5).ToList(); // Последние 5 копий
}
```

### Получение информации о файле

```csharp
public async Task<BackupFileInfo?> GetBackupInfoAsync(string fileName)
{
    return await _backupService.GetBackupFileInfoAsync(fileName);
}
```

## Устранение неполадок

### pg_dump не найден

Если возникает ошибка "pg_dump не найден":

1. Убедитесь, что PostgreSQL установлен
2. Проверьте, что `pg_dump` доступен в PATH
3. Укажите полный путь к `pg_dump` в коде сервиса

### Ошибки доступа к базе данных

1. Проверьте строки подключения в `appsettings.json`
2. Убедитесь, что пользователь имеет права на создание резервных копий
3. Проверьте сетевое подключение к серверу PostgreSQL

### Файлы не сохраняются в MemoryStorage

1. Убедитесь, что MemoryStorage сервис работает
2. Проверьте логи на наличие ошибок
3. Убедитесь, что у приложения достаточно памяти для хранения файлов

## Преимущества использования MemoryStorage

1. **Быстрый доступ** - файлы хранятся в памяти
2. **Автоматическая очистка** - файлы удаляются после скачивания
3. **Безопасность** - нет доступа к файловой системе
4. **Масштабируемость** - легко добавить дополнительные хранилища
5. **Интеграция** - использует существующую инфраструктуру MemoryStorage
