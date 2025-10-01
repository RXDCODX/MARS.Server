# TwitchMichaelJacksonRewardService

Сервис для обработки награды MichaelJackson на Twitch, который активирует эффект MichaelJackson при активации награды за 155 поинтов.

## Описание

`TwitchMichaelJacksonRewardService` - это управляемый сервис, который:

- Отслеживает активацию наград за баллы канала на Twitch
- При активации награды за 155 поинтов отправляет метод `MichaelJackson` в SignalR хаб
- Логирует все активации для мониторинга

## Конфигурация

### Параметры награды

- **Стоимость награды**: 155 поинтов
- **Канал**: Автоматически определяется через `TwitchExstension.Channel`

### Настройки

Сервис может быть включен/выключен через свойство `IsServiceActive`:

```csharp
// Выключение сервиса
service.IsServiceActive = false;

// Включение сервиса
service.IsServiceActive = true;
```

## Принцип работы

1. **Инициализация**: Сервис подписывается на события `ChannelPointsCustomRewardRedemptionAdd` при запуске
2. **Обработка наград**: При получении события проверяется стоимость награды (155 поинтов)
3. **Активация эффекта**: Если награда соответствует условиям, отправляется метод `MichaelJackson()` в хаб
4. **Логирование**: Все действия логируются для мониторинга и отладки

## Зависимости

- `IHubContext<TelegramusHub, ITelegramusHub>` - для отправки SignalR сообщений
- `EventSubWebsocketClient` - для получения событий Twitch
- `ILogger<TwitchMichaelJacksonRewardService>` - для логирования

## Использование

### Автоматическая регистрация

Сервис автоматически регистрируется в DI контейнере при запуске приложения:

```csharp
// В StartupEstensions.cs
services.AddSingleton<TwitchMichaelJacksonRewardService>();
services.AddHostedService(sp => sp.GetRequiredService<TwitchMichaelJacksonRewardService>());
```

## Логирование

Сервис логирует следующие события:

- **Информация**: Активация награды пользователем
- **Информация**: Успешная активация эффекта MichaelJackson
- **Ошибки**: Проблемы при обработке награды

### Примеры логов

```
info: TwitchMichaelJacksonRewardService[0]
      MichaelJackson награда активирована пользователем username за 155 поинтов

info: TwitchMichaelJacksonRewardService[0]
      MichaelJackson эффект активирован для пользователя username
```

## Безопасность

- Проверка канала: Сервис реагирует только на награды с канала `TwitchExstension.Channel`
- Проверка стоимости: Обрабатываются только награды за 155 поинтов
- Обработка ошибок: Все исключения перехватываются и логируются

## Мониторинг

### Статус сервиса

```csharp
var isActive = service.IsServiceActive;
Console.WriteLine($"MichaelJackson сервис активен: {isActive}");
```

## Тестирование

### Unit тесты

```csharp
[Test]
public async Task OnChannelPointsCustomRewardRedemption_With155Points_ShouldSendMichaelJackson()
{
    // Arrange
    var mockHubContext = new Mock<IHubContext<TelegramusHub, ITelegramusHub>>();
    var mockClients = new Mock<IHubClients>();
    var mockAll = new Mock<IClientProxy>();
    
    mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
    mockClients.Setup(x => x.All).Returns(mockAll.Object);
    
    var service = new TwitchMichaelJacksonRewardService(mockHubContext.Object, ...);
    
    // Act
    var args = CreateTestArgs(155);
    await service.OnChannelPointsCustomRewardRedemption(null, args);
    
    // Assert
    mockAll.Verify(x => x.MichaelJackson(), Times.Once);
}
```

## Заключение

`TwitchMichaelJacksonRewardService` предоставляет простой и надежный способ обработки награды MichaelJackson на Twitch. Сервис легко настраивается, мониторится и расширяется для новых потребностей.

### Ключевые особенности

- ✅ Автоматическая обработка наград за 155 поинтов
- ✅ Отправка SignalR сообщений в хаб
- ✅ Логирование всех действий
- ✅ Легкое расширение функциональности
- ✅ Обработка ошибок и исключений
