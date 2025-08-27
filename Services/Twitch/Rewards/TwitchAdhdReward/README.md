# TwitchAdhdService

Сервис для обработки награды ADHD на Twitch, который активирует эффект ADHD на 60 секунд при активации награды за 2002 поинта.

## Описание

`TwitchAdhdService` - это управляемый сервис, который:

- Отслеживает активацию наград за баллы канала на Twitch
- При активации награды за 2002 поинта отправляет метод `Adhd` в SignalR хаб
- Активирует эффект ADHD на 60 секунд
- Логирует все активации для мониторинга

## Конфигурация

### Параметры награды

- **Стоимость награды**: 2002 поинта
- **Длительность эффекта**: 60 секунд
- **Канал**: Автоматически определяется через `TwitchExstension.Channel`

### Настройки

Сервис наследует настройки от `ManagedServiceBase` и может быть включен/выключен через:

```csharp
// Включение сервиса
serviceManager.EnableService("twitchadhd");

// Выключение сервиса
serviceManager.DisableService("twitchadhd");
```

## Принцип работы

1. **Инициализация**: Сервис подписывается на события `ChannelPointsCustomRewardRedemptionAdd` при запуске
2. **Обработка наград**: При получении события проверяется стоимость награды (2002 поинта)
3. **Активация эффекта**: Если награда соответствует условиям, отправляется метод `Adhd(60)` в хаб
4. **Логирование**: Все действия логируются для мониторинга и отладки

## Зависимости

- `IHubContext<TelegramusHub, ITelegramusHub>` - для отправки SignalR сообщений
- `IHostApplicationLifetime` - для управления жизненным циклом приложения
- `EventSubWebsocketClient` - для получения событий Twitch
- `ManagedServiceBase` - базовый класс для управляемых сервисов

## Использование

### Автоматическая регистрация

Сервис автоматически регистрируется в DI контейнере при запуске приложения:

```csharp
// В StartupEstensions.cs
services.AddSingleton<TwitchAdhdService>();
services.AddHostedService(sp => sp.GetRequiredService<TwitchAdhdService>());
```

### Ручное управление

```csharp
public class SomeController : ControllerBase
{
    private readonly IServiceManager _serviceManager;

    public SomeController(IServiceManager serviceManager)
    {
        _serviceManager = serviceManager;
    }

    [HttpPost("adhd/enable")]
    public IActionResult EnableAdhd()
    {
        _serviceManager.EnableService("twitchadhd");
        return Ok("ADHD сервис включен");
    }

    [HttpPost("adhd/disable")]
    public IActionResult DisableAdhd()
    {
        _serviceManager.DisableService("twitchadhd");
        return Ok("ADHD сервис выключен");
    }
}
```

## Логирование

Сервис логирует следующие события:

- **Информация**: Активация награды пользователем
- **Информация**: Успешная активация эффекта ADHD
- **Ошибки**: Проблемы при обработке награды

### Примеры логов

```
info: TwitchAdhdService[0]
      ADHD награда активирована пользователем username за 2002 поинтов

info: TwitchAdhdService[0]
      ADHD эффект активирован на 60 секунд для пользователя username
```

## Безопасность

- Проверка канала: Сервис реагирует только на награды с канала `TwitchExstension.Channel`
- Проверка стоимости: Обрабатываются только награды за 2002 поинта
- Обработка ошибок: Все исключения перехватываются и логируются

## Мониторинг

### Статус сервиса

```csharp
var status = await _serviceManager.GetServiceStatusAsync("twitchadhd");
Console.WriteLine($"ADHD сервис активен: {status.IsActive}");
```

### Метрики

Сервис предоставляет базовые метрики через `ManagedServiceBase`:

- `ServiceName`: "twitchadhd"
- `DisplayName`: "Twitch ADHD"
- `Description`: "Сервис для обработки награды ADHD на Twitch"
- `IsServiceActive`: Текущий статус активности

## Расширение функциональности

### Добавление новых параметров

```csharp
public class TwitchAdhdService
{
    // Добавить новые константы
    private const int NewRewardCost = 3000;
    private const int NewDurationSeconds = 120;

    // Добавить новую логику обработки
    private async Task ProcessNewReward(ChannelPointsCustomRewardRedemption twEvent)
    {
        if (twEvent.Reward.Cost == NewRewardCost)
        {
            await hubContext.Clients.All.Adhd(NewDurationSeconds);
        }
    }
}
```

### Добавление пользовательского ввода

```csharp
// В обработчике награды
if (!string.IsNullOrWhiteSpace(twEvent.UserInput))
{
    if (int.TryParse(twEvent.UserInput, out var customDuration))
    {
        customDuration = Math.Clamp(customDuration, 10, 300); // Ограничение 10-300 секунд
        await hubContext.Clients.All.Adhd(customDuration);
    }
}
```

## Тестирование

### Unit тесты

```csharp
[Test]
public async Task OnChannelPointsCustomRewardRedemption_With2002Points_ShouldSendAdhd()
{
    // Arrange
    var mockHubContext = new Mock<IHubContext<TelegramusHub, ITelegramusHub>>();
    var mockClients = new Mock<IHubClients>();
    var mockAll = new Mock<IClientProxy>();
    
    mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
    mockClients.Setup(x => x.All).Returns(mockAll.Object);
    
    var service = new TwitchAdhdService(mockHubContext.Object, ...);
    
    // Act
    var args = CreateTestArgs(2002);
    await service.OnChannelPointsCustomRewardRedemption(null, args);
    
    // Assert
    mockAll.Verify(x => x.Adhd(60), Times.Once);
}
```

### Интеграционные тесты

```csharp
[Test]
public async Task Service_ShouldBeRegisteredInDI()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddTwitchServices(); // Метод расширения
    
    // Act
    var serviceProvider = services.BuildServiceProvider();
    var service = serviceProvider.GetService<TwitchAdhdService>();
    
    // Assert
    Assert.IsNotNull(service);
}
```

## Заключение

`TwitchAdhdService` предоставляет простой и надежный способ обработки награды ADHD на Twitch. Сервис легко настраивается, мониторится и расширяется для новых потребностей.

### Ключевые особенности

- ✅ Автоматическая обработка наград за 2002 поинта
- ✅ Отправка SignalR сообщений в хаб
- ✅ Логирование всех действий
- ✅ Управление через ServiceManager
- ✅ Легкое расширение функциональности
- ✅ Обработка ошибок и исключений
