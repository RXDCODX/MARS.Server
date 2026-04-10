# Миграция на WTelegramClientService

## Проблема
При работе с WTelegram может возникнуть ошибка `AUTH_KEY_UNREGISTERED` (код 401), которая означает, что сессия авторизации стала невалидной. Это требует повторной авторизации.

## Решение
Создан сервис-обертка `WTelegramClientService`, который автоматически обрабатывает эту ошибку и выполняет переавторизацию.

## Использование

### Базовое использование (рекомендуется)

```csharp
public class MyTelegramService
{
    private readonly WTelegramClientService _clientService;
    private readonly ILogger<MyTelegramService> _logger;

    public MyTelegramService(
        WTelegramClientService clientService,
        ILogger<MyTelegramService> logger)
    {
        _clientService = clientService;
        _logger = logger;
    }

    // Использование с автоматической переавторизацией
    public async Task<Messages_Chats> GetChatsAsync()
    {
        return await _clientService.ExecuteWithReauthAsync(async client =>
        {
            var chats = await client.Messages_GetAllChats();
            return chats;
        });
    }

    // Для void методов
    public async Task SendMessageAsync(long chatId, string message)
    {
        await _clientService.ExecuteWithReauthAsync(async client =>
        {
            await client.SendMessageAsync(chatId, message);
        });
    }
}
```

### Прямое получение клиента (если нужно)

```csharp
public async Task DirectClientUsageAsync()
{
    var client = await _clientService.GetClientAsync();
    
    try
    {
        // Работаем с клиентом напрямую
        var me = client.User;
    }
    catch (RpcException ex) when (ex.Code == 401)
    {
        // Можно вручную вызвать переавторизацию
        await _clientService.ReLoginAsync();
        client = await _clientService.GetClientAsync();
    }
}
```

### Принудительная переавторизация

```csharp
// Если нужно принудительно выполнить переавторизацию
await _clientService.ReLoginAsync();
```

## Миграция существующего кода

### Было:
```csharp
public class MyService
{
    private readonly WTelegram.Client _client;
    
    public MyService(WTelegram.Client client)
    {
        _client = client;
    }
    
    public async Task DoSomethingAsync()
    {
        var chats = await _client.Messages_GetAllChats();
    }
}
```

### Стало (вариант 1 - рекомендуется):
```csharp
public class MyService
{
    private readonly WTelegramClientService _clientService;
    
    public MyService(WTelegramClientService clientService)
    {
        _clientService = clientService;
    }
    
    public async Task DoSomethingAsync()
    {
        await _clientService.ExecuteWithReauthAsync(async client =>
        {
            var chats = await client.Messages_GetAllChats();
            // Работаем с chats
        });
    }
}
```

### Стало (вариант 2 - для обратной совместимости):
```csharp
public class MyService
{
    private readonly WTelegramClientService _clientService;
    
    public MyService(WTelegramClientService clientService)
    {
        _clientService = clientService;
    }
    
    public async Task DoSomethingAsync()
    {
        var client = await _clientService.GetClientAsync();
        
        try
        {
            var chats = await client.Messages_GetAllChats();
        }
        catch (RpcException ex) when (ex.Code == 401 && ex.Message.Contains("AUTH_KEY_UNREGISTERED"))
        {
            // Переавторизация произойдет автоматически при следующем вызове
            await _clientService.ReLoginAsync();
            throw; // или повторить запрос
        }
    }
}
```

## Процесс переавторизации

При возникновении ошибки `AUTH_KEY_UNREGISTERED`:

1. Старая сессия удаляется
2. Создается новый клиент
3. Начинается процесс авторизации:
   - Автоматически используется номер телефона из конфигурации
   - Если требуется код верификации - запрашивается через консоль
   - Если требуется имя - используется из конфигурации
   - Если требуется пароль 2FA - используется из конфигурации

## Конфигурация

```json
{
  "AppBase": {
    "WTelegram": {
      "AppId": 12345678,
      "ApiHash": "your-api-hash",
      "PhoneNumber": "+1234567890",
      "FirstNameLastName": "John Doe",
      "Password": "your-2fa-password"
    }
  }
}
```

## Логирование

Сервис логирует все важные события:
- Начало/завершение авторизации
- Обнаружение `AUTH_KEY_UNREGISTERED`
- Запросы кода верификации
- Успешная авторизация

Проверяйте логи категории `WTelegramClientService` и `WTelegram`.
