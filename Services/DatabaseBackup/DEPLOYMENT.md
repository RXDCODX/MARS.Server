# Инструкции по развертыванию сервиса резервного копирования (MemoryStorage)

## Предварительные требования

### 1. PostgreSQL

- Установлен PostgreSQL на сервере
- `pg_dump` доступен в системе
- Пользователь имеет права на создание резервных копий

### 2. MemoryStorage

- MemoryStorage сервис настроен и работает
- Достаточно оперативной памяти для хранения резервных копий
- Контроллер `/memory` настроен и доступен

### 3. Права доступа

```sql
-- Создание пользователя с правами на резервное копирование
CREATE USER backup_user WITH PASSWORD 'secure_password';

-- Предоставление прав на создание резервных копий
GRANT CONNECT ON DATABASE dev TO backup_user;
GRANT CONNECT ON DATABASE prod TO backup_user;
GRANT USAGE ON SCHEMA public TO backup_user;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO backup_user;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO backup_user;

-- Для новых таблиц
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO backup_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON SEQUENCES TO backup_user;
```

### 4. Проверка pg_dump

```bash
# Проверка доступности pg_dump
pg_dump --version

# Если не найден, добавьте в PATH
export PATH=$PATH:/usr/lib/postgresql/14/bin
# или для Windows
set PATH=%PATH%;C:\Program Files\PostgreSQL\14\bin
```

## Развертывание

### 1. Сборка проекта

```bash
# В директории MARS.Server
dotnet build --configuration Release
```

### 2. Проверка конфигурации

Убедитесь, что в `appsettings.json` или `appsettings.Production.json` настроены строки подключения:

```json
{
  "ConnectionStrings": {
    "Dev_Path": "User ID=backup_user;Password=secure_password;Host=localhost;Port=5432;Database=dev;",
    "Prod_Path": "User ID=backup_user;Password=secure_password;Host=localhost;Port=5432;Database=prod;"
  }
}
```

### 3. Проверка MemoryStorage

Убедитесь, что MemoryStorage работает:

```bash
# Проверка доступности контроллера
curl -I http://localhost:5000/memory/test

# Проверка API резервного копирования
curl -X GET http://localhost:5000/api/DatabaseBackup/status
```

### 4. Запуск приложения

```bash
# Запуск в production
dotnet run --configuration Release --environment Production

# Или как Windows Service
sc create "MARS.Server" binPath="C:\path\to\MARS.Server.exe"
sc start "MARS.Server"
```

## Настройка автоматизации

### 1. Планировщик задач Windows

```batch
# Создание задачи для ежедневного резервного копирования
schtasks /create /tn "MARS Database Backup" /tr "curl -X POST http://localhost:5000/api/DatabaseBackup/create?databaseName=prod" /sc daily /st 02:00

# Создание задачи для очистки старых копий
schtasks /create /tn "MARS Backup Cleanup" /tr "curl -X POST http://localhost:5000/api/DatabaseBackup/cleanup?keepCount=10" /sc daily /st 03:00
```

### 2. Cron (Linux)

```bash
# Ежедневное резервное копирование в 2:00
0 2 * * * curl -X POST "http://localhost:5000/api/DatabaseBackup/create?databaseName=prod"

# Очистка старых копий в 3:00
0 3 * * * curl -X POST "http://localhost:5000/api/DatabaseBackup/cleanup?keepCount=10"
```

### 3. PowerShell скрипт для Windows

```powershell
# Создайте файл backup-scheduler.ps1
$BaseUrl = "http://localhost:5000/api/DatabaseBackup"

# Создание резервной копии
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/create?databaseName=prod" -Method POST
    Write-Host "Резервная копия создана: $($response.downloadUrl)" -ForegroundColor Green
} catch {
    Write-Host "Ошибка создания резервной копии: $($_.Exception.Message)" -ForegroundColor Red
}

# Очистка старых копий
try {
    $cleanup = Invoke-RestMethod -Uri "$BaseUrl/cleanup?keepCount=10" -Method POST
    Write-Host "Очищено $($cleanup.deletedCount) старых копий" -ForegroundColor Green
} catch {
    Write-Host "Ошибка очистки: $($_.Exception.Message)" -ForegroundColor Red
}
```

## Мониторинг

### 1. Логирование

Сервис автоматически логирует все операции. Проверяйте логи:

```bash
# Windows Event Viewer
eventvwr.msc

# Или логи приложения
tail -f /path/to/app/logs/app.log
```

### 2. Проверка статуса

```bash
# Проверка статуса через API
curl -X GET "http://localhost:5000/api/DatabaseBackup/status"

# Проверка списка резервных копий
curl -X GET "http://localhost:5000/api/DatabaseBackup/list"
```

### 3. Мониторинг памяти

```bash
# Проверка использования памяти приложением
# Windows
wmic process where "name='MARS.Server.exe'" get WorkingSetSize

# Linux
ps -o pid,vsz,rss,comm -p $(pgrep -f MARS.Server)
```

## Безопасность

### 1. Ограничение доступа к API

```csharp
// В Program.cs добавьте аутентификацию
services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options => {
        // Настройка JWT
    });

// В контроллере добавьте атрибут
[Authorize]
public class DatabaseBackupController : ControllerBase
```

### 2. Ограничение по IP

```csharp
// В appsettings.json
{
  "AllowedIPs": ["192.168.1.100", "10.0.0.50"]
}

// В контроллере
[ServiceFilter(typeof(IPFilterAttribute))]
public class DatabaseBackupController : ControllerBase
```

### 3. Шифрование резервных копий

```bash
# Создание зашифрованной резервной копии
pg_dump -h localhost -U backup_user -d prod | gpg -e -r admin@company.com > backup_prod_$(date +%Y%m%d_%H%M%S).sql.gpg

# Расшифровка
gpg -d backup_prod_20241201_143022.sql.gpg > backup_prod_20241201_143022.sql
```

## Восстановление

### 1. Восстановление из резервной копии

```bash
# Скачивание резервной копии
curl -o backup.sql "http://localhost:5000/memory/backup_prod_20241201_143022.sql"

# Восстановление базы данных
psql -h localhost -U postgres -d prod < backup.sql

# Или через pg_restore для бинарных форматов
pg_restore -h localhost -U postgres -d prod backup_prod_20241201_143022.dump
```

### 2. Проверка целостности

```bash
# Проверка размера восстановленной базы
psql -h localhost -U postgres -d prod -c "SELECT pg_size_pretty(pg_database_size('prod'));"

# Проверка количества таблиц
psql -h localhost -U postgres -d prod -c "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public';"
```

## Устранение неполадок

### 1. pg_dump не найден

```bash
# Поиск pg_dump в системе
find /usr -name "pg_dump" 2>/dev/null
find /opt -name "pg_dump" 2>/dev/null

# Добавление в PATH
echo 'export PATH=$PATH:/usr/lib/postgresql/14/bin' >> ~/.bashrc
source ~/.bashrc
```

### 2. Ошибки доступа к базе данных

```bash
# Проверка подключения
psql -h localhost -U backup_user -d dev -c "SELECT 1;"

# Проверка прав пользователя
psql -h localhost -U postgres -d dev -c "\du backup_user"
```

### 3. Проблемы с MemoryStorage

```bash
# Проверка доступности MemoryStorage
curl -I http://localhost:5000/memory/test

# Проверка логов приложения
tail -f /path/to/app/logs/app.log | grep -i memory

# Проверка использования памяти
free -h  # Linux
wmic OS get TotalVisibleMemorySize,FreePhysicalMemory  # Windows
```

### 4. Недостаточно памяти

```bash
# Очистка старых резервных копий
curl -X POST "http://localhost:5000/api/DatabaseBackup/cleanup?keepCount=5"

# Проверка свободной памяти
free -h  # Linux
wmic OS get FreePhysicalMemory  # Windows
```

## Тестирование развертывания

### 1. Проверка API

```bash
# Тест создания резервной копии
curl -X POST "http://localhost:5000/api/DatabaseBackup/create?databaseName=dev"

# Тест получения списка
curl -X GET "http://localhost:5000/api/DatabaseBackup/list"
```

### 2. Проверка MemoryStorage

```bash
# Проверка создания файла в MemoryStorage
curl -X GET "http://localhost:5000/api/DatabaseBackup/list" | jq '.backups[0].fileName'

# Прямое скачивание через MemoryStorage
curl -o test_backup.sql "http://localhost:5000/memory/backup_dev_20241201_143022.sql"
```

### 3. Проверка логов

```bash
# Поиск ошибок в логах
grep -i error /path/to/app/logs/app.log | tail -10

# Поиск успешных операций
grep -i "резервная копия создана" /path/to/app/logs/app.log | tail -5
```

## Преимущества MemoryStorage

1. **Быстрый доступ** - файлы хранятся в памяти
2. **Автоматическая очистка** - файлы удаляются после скачивания
3. **Безопасность** - нет доступа к файловой системе
4. **Масштабируемость** - легко добавить дополнительные хранилища
5. **Интеграция** - использует существующую инфраструктуру MemoryStorage
