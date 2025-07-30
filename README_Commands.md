# Сервис Команд MARS.Server

## Обзор

Новый сервис команд позволяет использовать команды бота через API. Все команды принимают строку ввода и возвращают строку с ответом. Система поддерживает атрибуты для описания параметров команд, что позволяет фронтенду автоматически создавать формы ввода.

## Архитектура

### Основные компоненты:

1. **ICommandService** - интерфейс для выполнения команд
2. **CommandExecutorService** - основная реализация сервиса команд
3. **BaseCommand** - базовый класс для всех команд
4. **CommandsController** - API контроллер для фронтенда
5. **CommandParameterAttribute** - атрибут для описания параметров команд

### Структура команд:

```
Services/CommandExecutor/
├── ICommandService.cs
├── CommandExecutorService.cs
└── Entitys/
    ├── Attribute/
    │   ├── CommandParameterAttribute.cs
    │   └── Commands/
    │       ├── HelpCommand.cs
    │       ├── StartCommand.cs
    │       ├── FramedataCommand.cs
    │       ├── CommandsListCommand.cs
    │       └── ExampleCommand.cs
    └── Commands/
        └── BaseCommand.cs
```

## Использование

### Через API:

#### Получить список команд:
```http
GET /api/commands/list?includeAdminCommands=false
```

#### Получить информацию о командах с параметрами:
```http
GET /api/commands/info?includeAdminCommands=false
```

#### Получить параметры конкретной команды:
```http
GET /api/commands/{commandName}/parameters
```

#### Выполнить команду:
```http
POST /api/commands/execute
Content-Type: application/json

{
  "commandName": "help",
  "input": ""
}
```

#### Пример с параметрами:
```http
POST /api/commands/execute
Content-Type: application/json

{
  "commandName": "framedata",
  "input": "jin kazama 1,2,3"
}
```

## Создание команд с параметрами

### Атрибут CommandParameter

```csharp
[CommandParameter("name", "Имя параметра", "тип", обязательный, "значение_по_умолчанию")]
```

**Параметры:**
- `name` - имя параметра
- `description` - описание параметра
- `type` - тип данных (string, int, bool, double, long)
- `required` - обязательный ли параметр (по умолчанию true)
- `defaultValue` - значение по умолчанию (опционально)

### Пример команды с параметрами:

```csharp
[Description("Пример команды с несколькими параметрами")]
[CommandParameter("name", "Имя пользователя", "string", true)]
[CommandParameter("age", "Возраст", "int", false, "18")]
[CommandParameter("message", "Сообщение", "string", false)]
public class ExampleCommand : BaseCommand
{
    public override string CommandName => "example";
    public override string Description => "Пример команды с несколькими параметрами";
    public override bool IsAdminCommand => false;

    public override Task<string> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        var name = parameters["name"].ToString() ?? "Неизвестно";
        var age = Convert.ToInt32(parameters["age"]);
        var message = parameters.TryGetValue("message", out var msgObj) ? msgObj.ToString() : "Привет!";

        return Task.FromResult($"Привет, {name}! Возраст: {age}. Сообщение: {message}");
    }
}
```

### Разбор параметров

Система автоматически разбирает входную строку на параметры:

```
Входная строка: "Иван 25 Привет всем!"
Результат:
- name: "Иван"
- age: 25
- message: "Привет всем!"
```

## API Endpoints

### GET /api/commands/info
Возвращает полную информацию о командах с параметрами.

**Ответ:**
```json
{
  "commands": [
    {
      "name": "example",
      "description": "Пример команды с несколькими параметрами",
      "isAdminCommand": false,
      "parameters": [
        {
          "name": "name",
          "description": "Имя пользователя",
          "type": "string",
          "required": true,
          "defaultValue": null
        },
        {
          "name": "age",
          "description": "Возраст",
          "type": "int",
          "required": false,
          "defaultValue": "18"
        }
      ]
    }
  ],
  "count": 1
}
```

### GET /api/commands/{commandName}/parameters
Возвращает параметры конкретной команды.

### POST /api/commands/execute
Выполняет команду с заданными параметрами.

## Демо

Откройте `http://localhost:5000/commands-demo.html` для тестирования API команд.

Демо страница показывает:
- Список всех команд с параметрами
- Автоматическое создание форм ввода на основе параметров
- Выполнение команд через API
- Примеры использования

## Добавление новых команд

### 1. Создайте новый класс команды:

```csharp
[Description("Описание команды")]
[CommandParameter("param1", "Описание параметра 1", "string", true)]
[CommandParameter("param2", "Описание параметра 2", "int", false, "0")]
public class MyNewCommand : BaseCommand
{
    public override string CommandName => "mycommand";
    public override string Description => "Описание команды";
    public override bool IsAdminCommand => false;

    public override Task<string> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        // Логика команды
        return Task.FromResult("Результат команды");
    }
}
```

### 2. Зарегистрируйте команду в CommandExecutorService:

```csharp
private void RegisterCommands(IDbContextFactory<AppDbContext> factory, Tekken8FrameData frameData)
{
    // Существующие команды...
    RegisterCommand(new MyNewCommand());
}
```

## Преимущества новой архитектуры:

- **Универсальность**: Команды работают через API без привязки к Telegram
- **Автоматические формы**: Фронтенд может создавать формы ввода на основе атрибутов
- **Типизация**: Поддержка различных типов данных (string, int, bool, double, long)
- **Валидация**: Автоматическая проверка обязательных параметров
- **Значения по умолчанию**: Поддержка значений по умолчанию для необязательных параметров
- **Гибкость**: Можно вводить параметры как через отдельные поля, так и одной строкой
- **Документация**: Автоматическая генерация документации по параметрам 