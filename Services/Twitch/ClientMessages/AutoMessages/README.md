# AutoMessages Service

Сервис для управления автоматическими сообщениями Twitch с полноценным CRUD API.

## Архитектура

### Компоненты

1. **AutoMessage** - Entity класс для базы данных
2. **AutoMessagesService** - Бизнес-логика CRUD операций
3. **AutoMessagesController** - API контроллер для HTTP endpoints
4. **DTO классы** - Объекты передачи данных

### Структура файлов

```
Services/Twitch/ClientMessages/AutoMessages/
├── DTOs/
│   ├── AutoMessageDto.cs
│   ├── CreateAutoMessageRequest.cs
│   └── UpdateAutoMessageRequest.cs
├── Entitys/
│   └── AutoMessage.cs
├── Extensions/
│   └── AutoMessagesServiceExtensions.cs
├── Interfaces/
│   └── IAutoMessagesService.cs
├── Services/
│   └── AutoMessagesService.cs
├── AutoMessagesController.cs (BackgroundService)
└── README.md
```

## API Endpoints

### GET /api/automessages

Получить все автоматические сообщения

**Ответ:**

```json
[
  {
    "id": "guid",
    "message": "string"
  }
]
```

### GET /api/automessages/{id}

Получить автоматическое сообщение по ID

**Параметры:**

- `id` (Guid) - ID сообщения

**Ответ:**

```json
{
  "id": "guid",
  "message": "string"
}
```

### POST /api/automessages

Создать новое автоматическое сообщение

**Тело запроса:**

```json
{
  "message": "string"
}
```

**Ответ:** 201 Created с созданным объектом

### PUT /api/automessages/{id}

Обновить автоматическое сообщение

**Параметры:**

- `id` (Guid) - ID сообщения

**Тело запроса:**

```json
{
  "message": "string"
}
```

**Ответ:** 200 OK с обновленным объектом

### DELETE /api/automessages/{id}

Удалить автоматическое сообщение

**Параметры:**

- `id` (Guid) - ID сообщения

**Ответ:** 204 No Content

## Использование

### Регистрация сервиса

Сервис автоматически регистрируется в DI контейнере через extension метод:

```csharp
services.AddAutoMessagesService();
```

### Инъекция зависимостей

```csharp
public class MyController(IAutoMessagesService autoMessagesService)
{
    // Использование сервиса
}
```

## Интеграция с существующим функционалом

Существующий `AutoMessagesController` (BackgroundService) продолжает работать для автоматической отправки сообщений в Twitch чат. Новый API контроллер предоставляет возможность управлять этими сообщениями через веб-интерфейс.

### Логика автоматической отправки

- Отправка происходит при достижении 70 сообщений в чате
- Интервал между отправками: 45 минут
- Используется очередь из последних 3 отправленных сообщений для избежания повторов

## Стиль кодирования

Сервис следует правилам проекта:

- Стиль "один вход - один выход"
- Использование `AsNoTracking()` для Entity Framework
- Позитивный сценарий с ранним возвращением для негативных случаев
- Логирование всех операций
- Обработка исключений
