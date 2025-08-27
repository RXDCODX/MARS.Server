# Примеры использования API резервного копирования через Swagger (MemoryStorage)

## Доступ к Swagger

После запуска приложения Swagger будет доступен по адресу:

```
http://localhost:5000/ui
```

## Endpoints для тестирования

### 1. Создание резервной копии

**Endpoint:** `POST /api/DatabaseBackup/create`

**Параметры:**

- `databaseName` (query): `dev` или `prod`

**Пример запроса:**

```json
// URL: POST /api/DatabaseBackup/create?databaseName=dev
// Body: пустой
```

**Ожидаемый ответ:**

```json
{
  "success": true,
  "message": "Резервная копия базы данных dev создана успешно",
  "downloadUrl": "/memory/backup_dev_20241201_143022.sql",
  "fileName": "backup_dev_20241201_143022.sql",
  "createdAt": "2024-12-01T14:30:22.123Z"
}
```

### 2. Список резервных копий

**Endpoint:** `GET /api/DatabaseBackup/list`

**Параметры:** отсутствуют

**Пример запроса:**

```json
// URL: GET /api/DatabaseBackup/list
// Body: пустой
```

**Ожидаемый ответ:**

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

### 3. Статус резервного копирования

**Endpoint:** `GET /api/DatabaseBackup/status`

**Параметры:** отсутствуют

**Пример запроса:**

```json
// URL: GET /api/DatabaseBackup/status
// Body: пустой
```

**Ожидаемый ответ:**

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

### 4. Скачивание резервной копии через API

**Endpoint:** `GET /api/DatabaseBackup/download`

**Параметры:**

- `fileName` (query): имя файла резервной копии

**Пример запроса:**

```json
// URL: GET /api/DatabaseBackup/download?fileName=backup_dev_20241201_143022.sql
// Body: пустой
```

**Ожидаемый ответ:** Файл SQL для скачивания

### 5. Прямое скачивание через MemoryStorage

**Endpoint:** `GET /memory/{fileName}`

**Параметры:**

- `fileName` (path): имя файла резервной копии

**Пример запроса:**

```json
// URL: GET /memory/backup_dev_20241201_143022.sql
// Body: пустой
```

**Ожидаемый ответ:** Файл SQL для скачивания (автоматически удаляется после скачивания)

### 6. Очистка старых резервных копий

**Endpoint:** `POST /api/DatabaseBackup/cleanup`

**Параметры:**

- `keepCount` (query): количество копий для сохранения (по умолчанию 10)

**Пример запроса:**

```json
// URL: POST /api/DatabaseBackup/cleanup?keepCount=5
// Body: пустой
```

**Ожидаемый ответ:**

```json
{
  "success": true,
  "message": "Очистка завершена. Удалено 3 старых резервных копии",
  "deletedCount": 3,
  "keepCount": 5
}
```

## Пошаговое тестирование

### Шаг 1: Создание резервной копии

1. Откройте Swagger UI
2. Найдите endpoint `POST /api/DatabaseBackup/create`
3. Нажмите "Try it out"
4. Введите `databaseName`: `dev`
5. Нажмите "Execute"
6. Запомните `fileName` и `downloadUrl` из ответа

### Шаг 2: Просмотр списка

1. Найдите endpoint `GET /api/DatabaseBackup/list`
2. Нажмите "Try it out"
3. Нажмите "Execute"
4. Убедитесь, что созданная копия присутствует в списке

### Шаг 3: Проверка статуса

1. Найдите endpoint `GET /api/DatabaseBackup/status`
2. Нажмите "Try it out"
3. Нажмите "Execute"
4. Проверьте общее количество и размер

### Шаг 4: Скачивание файла через API

1. Найдите endpoint `GET /api/DatabaseBackup/download`
2. Нажмите "Try it out"
3. Введите `fileName` из шага 1
4. Нажмите "Execute"
5. Файл должен начать скачиваться

### Шаг 5: Прямое скачивание через MemoryStorage

1. Скопируйте `downloadUrl` из шага 1
2. Откройте новый вкладку в браузере
3. Перейдите по URL (например: `http://localhost:5000/memory/backup_dev_20241201_143022.sql`)
4. Файл должен начать скачиваться

### Шаг 6: Очистка (опционально)

1. Найдите endpoint `POST /api/DatabaseBackup/cleanup`
2. Нажмите "Try it out"
3. Введите `keepCount`: `5`
4. Нажмите "Execute"
5. Проверьте количество удаленных файлов

## Возможные ошибки

### 400 Bad Request

- `databaseName` не указан или имеет недопустимое значение
- `keepCount` меньше 1 или больше 100

### 404 Not Found

- Файл резервной копии не найден в MemoryStorage

### 500 Internal Server Error

- Ошибка при выполнении pg_dump
- Проблемы с MemoryStorage
- Ошибки подключения к базе данных

## Советы по тестированию

1. **Начните с создания резервной копии** - это основной функционал
2. **Проверьте логи** - все операции логируются
3. **Используйте разные базы данных** - протестируйте и `dev`, и `prod`
4. **Тестируйте прямое скачивание** - проверьте работу MemoryStorage
5. **Тестируйте граничные случаи** - пустые параметры, некорректные значения

## Особенности MemoryStorage

1. **Автоматическое удаление** - файлы удаляются после скачивания
2. **Быстрый доступ** - файлы хранятся в памяти
3. **Безопасность** - нет доступа к файловой системе
4. **Интеграция** - использует существующую инфраструктуру

## Проверка работы MemoryStorage

После создания резервной копии проверьте:

1. **Файл в списке** - должен появиться в `/api/DatabaseBackup/list`
2. **Прямой доступ** - должен быть доступен по `/memory/{filename}`
3. **Автоматическое удаление** - после скачивания файл должен исчезнуть из списка
