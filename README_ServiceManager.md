# MARS Server - Панель управления сервисами

## Описание

Панель управления сервисами для MARS Server позволяет управлять всеми фоновыми сервисами приложения через веб-интерфейс. Новая архитектура основана на `IHostedService` и `BackgroundService`, что обеспечивает более надежное управление сервисами.

## Архитектура

### Основные компоненты

1. **IServiceManager** - Интерфейс для управления сервисами
2. **ManagedServiceBase** - Базовый класс для управляемых сервисов (наследуется от BackgroundService)
3. **ServiceManager** - Реализация менеджера сервисов
4. **ServiceManagerController** - API контроллер
5. **ServiceState** - Сущность для хранения состояния сервисов в БД
6. **admin.html** - Веб-интерфейс панели управления

### Модели данных

- **ServiceStatus** - Перечисление статусов сервиса (Running, Stopped, Starting, Stopping, Error, Unknown)
- **ServiceInfo** - Информация о сервисе
- **ServiceLog** - Лог сервиса
- **ServiceState** - Состояние сервиса в базе данных

## Возможности

### Управление сервисами
- **Просмотр статуса** всех сервисов в реальном времени
- **Запуск/остановка** отдельных сервисов
- **Перезапуск** сервисов
- **Включение/отключение** сервисов (IsActive)
- **Просмотр логов** каждого сервиса
- **Сохранение состояния** в базе данных

### Поддерживаемые сервисы

#### Twitch сервисы
- **Twitch Authentication** - Аутентификация Twitch
- **Auto Messages** - Автоматические сообщения
- **Fumo Friday** - Сервис Fumo Friday
- **Hello Videos** - Приветственные видео
- **Media Alerts** - Медиа алерты
- **Mini Games** - Мини-игры (Tekken Victorina, Russian Roulette, Trivia)
- **Synthesizer** - Синтезатор речи
- **Waifu Rolls** - Вайфу роллы
- **Sound Request** - Запросы звуков
- **Clip Creator** - Создание клипов
- **Frame Data** - Данные кадров Tekken
- **Messages Hub** - Хаб сообщений
- **Screen Particles** - Частицы на экране
- **Rewards** - Награды Twitch

#### Основные сервисы
- **Random Meme Worker** - Рабочий случайных мемов
- **Random Meme Online** - Онлайн случайных мемов
- **Sound Request Backend** - Бэкенд звуковых запросов
- **Sound Request Playlist** - Плейлист звуковых запросов
- **Pyro Alerts** - Алерты Pyro
- **Waifu Roll** - Сервис вайфу роллов
- **Shikimori** - Сервис Shikimori
- **365 Genius** - Сервис 365 Genius
- **Honkai** - Сервис Honkai
- **Telegram Bot** - Telegram бот

## Использование

### Доступ к панели
1. Запустите MARS Server
2. Откройте браузер и перейдите по адресу: `http://localhost:5000/admin.html`
3. Панель автоматически загрузит статус всех сервисов

### Управление сервисами
1. **Просмотр статуса**: Статус каждого сервиса отображается цветной меткой
   - 🟢 **Running** - Сервис работает
   - 🔴 **Stopped** - Сервис остановлен
   - 🟡 **Starting/Stopping** - Сервис запускается/останавливается
   - 🔴 **Error** - Ошибка в работе сервиса
   - ⚪ **Unknown** - Статус неизвестен

2. **Управление**:
   - Нажмите **"Включить/Отключить"** для изменения активности сервиса
   - Нажмите **"Запустить"** для запуска остановленного сервиса
   - Нажмите **"Остановить"** для остановки работающего сервиса
   - Нажмите **"Перезапустить"** для перезапуска сервиса
   - Нажмите **"Логи"** для просмотра логов сервиса

3. **Информация о сервисе**:
   - Время запуска
   - Время последней активности
   - Описание сервиса
   - Статус активности

### Автообновление
- Статус сервисов автоматически обновляется каждые 30 секунд
- Нажмите кнопку 🔄 в правом нижнем углу для принудительного обновления

## API Endpoints

Панель использует следующие API endpoints:

### Получение всех сервисов
```
GET /api/servicemanager/services
```

### Получение статуса всех сервисов
```
GET /api/servicemanager/status
```

### Получение информации о сервисе
```
GET /api/servicemanager/service/{serviceName}
```

### Запуск сервиса
```
POST /api/servicemanager/service/{serviceName}/start
```

### Остановка сервиса
```
POST /api/servicemanager/service/{serviceName}/stop
```

### Перезапуск сервиса
```
POST /api/servicemanager/service/{serviceName}/restart
```

### Включение/отключение сервиса
```
POST /api/servicemanager/service/{serviceName}/active
Content-Type: application/json
Body: true/false
```

### Получение логов сервиса
```
GET /api/servicemanager/service/{serviceName}/logs?count=100
```

## Создание управляемого сервиса

Для создания нового управляемого сервиса:

1. **Наследуйтесь от ManagedServiceBase**:
```csharp
[ServiceName("my-service")]
public class MyManagedService : ManagedServiceBase
{
    public override string ServiceName => "my-service";
    public override string DisplayName => "My Service";
    public override string Description => "Описание моего сервиса";

    public MyManagedService(ILogger<MyManagedService> logger) : base(logger)
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Логика сервиса
    }

    protected override Dictionary<string, object> GetConfiguration()
    {
        return new Dictionary<string, object>
        {
            ["Key"] = "Value"
        };
    }
}
```

2. **Зарегистрируйте сервис в DI**:
```csharp
services.AddHostedService<MyManagedService>();
```

3. **Добавьте отображаемые имена** в `ServiceManager.cs`:
```csharp
private string GetServiceDisplayName(string serviceName)
{
    var displayNames = new Dictionary<string, string>
    {
        ["my-service"] = "My Service",
        // ... другие сервисы
    };
    
    return displayNames.GetValueOrDefault(serviceName, serviceName);
}
```

## База данных

### Таблица ServiceStates
- **Id** - Первичный ключ
- **ServiceName** - Название сервиса
- **DisplayName** - Отображаемое имя
- **Description** - Описание
- **IsActive** - Активен ли сервис
- **Status** - Статус сервиса
- **LastStartTime** - Время последнего запуска
- **LastActivity** - Время последней активности
- **CreatedAt** - Время создания записи
- **UpdatedAt** - Время последнего обновления
- **ConfigurationJson** - Конфигурация в JSON формате

### Миграция
Для создания миграции выполните:
```bash
dotnet ef migrations add AddServiceStates
dotnet ef database update
```

## Безопасность

⚠️ **Важно**: Панель управления предоставляет полный доступ к управлению сервисами. Рекомендуется:

1. Ограничить доступ к панели только для администраторов
2. Настроить аутентификацию и авторизацию
3. Использовать HTTPS в продакшене
4. Ограничить доступ по IP-адресам при необходимости

## Разработка

### Добавление нового сервиса
1. Создайте класс, наследующийся от `ManagedServiceBase`
2. Добавьте атрибут `[ServiceName("service-name")]`
3. Реализуйте абстрактные свойства и методы
4. Зарегистрируйте сервис в DI контейнере
5. Добавьте отображаемые имена в `ServiceManager`

### Расширение функциональности
- Добавьте новые методы в `IServiceManager`
- Реализуйте их в `ServiceManager`
- Добавьте соответствующие endpoints в контроллер
- Обновите веб-интерфейс

## Устранение неполадок

### Сервис не отображается
- Проверьте, что сервис зарегистрирован в DI контейнере
- Убедитесь, что сервис наследуется от `ManagedServiceBase` или является `IHostedService`
- Проверьте атрибут `[ServiceName]` или логику определения имени сервиса

### Ошибки API
- Проверьте логи приложения
- Убедитесь, что все зависимости зарегистрированы
- Проверьте права доступа к базе данных

### Проблемы с веб-интерфейсом
- Проверьте консоль браузера на наличие ошибок JavaScript
- Убедитесь, что файл `admin.html` доступен по адресу `/admin.html`
- Проверьте CORS настройки, если панель открывается с другого домена

### Проблемы с базой данных
- Убедитесь, что миграция `AddServiceStates` применена
- Проверьте подключение к базе данных
- Проверьте права доступа к таблице `ServiceStates`

## Лицензия

Этот проект является частью MARS Server и подчиняется тем же условиям лицензии. 