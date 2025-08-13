# Примеры использования команды ScrupFrameDataCommand

## 🚀 **Новые возможности команды**

Команда теперь поддерживает автоматический парсинг параметров через систему `CommandParameterInfo`, что позволяет:
- Автоматически разбирать параметры из текстового ввода
- Устанавливать значения по умолчанию
- Валидировать типы данных
- Предоставлять описания параметров для фронтенда

## 📝 **Алиасы команды**

Команду можно вызывать следующими способами:
- `/scrupframedata` - полное название
- `/scrap` - сокращенное название
- `/parse` - альтернативное название
- `/framedata` - тематическое название

## 🔧 **Параметры команды**

| Параметр | Тип | Описание | По умолчанию | Обязательный |
|----------|-----|----------|--------------|--------------|
| `source` | string | Источник данных: "wavu" или "tekkendocs" | "wavu" | ❌ |
| `characters` | string | Список персонажей через запятую | null (все персонажи) | ❌ |
| `requestDelay` | int | Задержка между запросами в секундах | 2 | ❌ |
| `characterDelay` | int | Задержка между персонажами в секундах | 5 | ❌ |
| `parseMoves` | bool | Парсить ли мувы | true | ❌ |
| `useStaging` | bool | Использовать ли staging service | true | ❌ |
| `maxRetries` | int | Максимальное количество попыток | 3 | ❌ |
| `timeout` | int | Таймаут HTTP запросов в секундах | 30 | ❌ |

## 💻 **Примеры использования через команды**

### Базовое использование (с параметрами по умолчанию)
```
/scrupframedata
/scrap
/parse
/framedata
```

### Настройка источника данных
```
/scrupframedata source:wavu
/scrap source:tekkendocs
```

### Парсинг конкретных персонажей
```
/scrupframedata characters:Kazuya,Heihachi,Jin
/scrap characters:Kazuya
```

### Настройка задержек
```
/scrupframedata requestDelay:5 characterDelay:10
/scrap requestDelay:1 characterDelay:2
```

### Парсинг только персонажей (без мувов)
```
/scrupframedata parseMoves:false
/scrap parseMoves:false
```

### Прямое добавление в базу данных (минуя staging)
```
/scrupframedata useStaging:false
/scrap useStaging:false
```

### Комплексная настройка
```
/scrupframedata source:wavu characters:Kazuya,Heihachi requestDelay:1 characterDelay:2 parseMoves:true useStaging:false timeout:30 maxRetries:3
```

## 🔄 **Программное использование**

### Базовое использование (с параметрами по умолчанию)
```csharp
// Без параметров - использует настройки по умолчанию
var result = await command.ExecuteAsync(new Dictionary<string, object>());
```

### Настройка задержек
```csharp
// Увеличить задержки для более "вежливого" парсинга
var parameters = new Dictionary<string, object>
{
    ["requestDelay"] = 5,        // 5 секунд между запросами
    ["characterDelay"] = 10      // 10 секунд между персонажами
};
```

### Парсинг только персонажей (без мувов)
```csharp
// Парсить только информацию о персонажах, без мувов
var parameters = new Dictionary<string, object>
{
    ["parseMoves"] = false       // Не парсить мувы
};
```

### Прямое добавление в базу данных (минуя staging)
```csharp
// Добавлять изменения напрямую в базу данных
var parameters = new Dictionary<string, object>
{
    ["useStaging"] = false       // Не использовать staging service
};
```

### Выбор источника данных
```csharp
// Парсить с Wavu.wiki
var parameters = new Dictionary<string, object>
{
    ["source"] = "wavu"
};

// Парсить с Tekkendocs.com
var parameters = new Dictionary<string, object>
{
    ["source"] = "tekkendocs"
};
```

### Парсинг конкретных персонажей
```csharp
// Парсить только определенных персонажей
var parameters = new Dictionary<string, object>
{
    ["characters"] = "Kazuya,Heihachi,Jin"
};
```

### Настройка таймаутов и повторных попыток
```csharp
// Увеличить таймаут и количество попыток
var parameters = new Dictionary<string, object>
{
    ["timeout"] = 60,            // 60 секунд таймаут
    ["maxRetries"] = 5           // 5 попыток для каждого запроса
};
```

### Комплексный пример
```csharp
// Полная настройка для быстрого парсинга конкретных персонажей
var parameters = new Dictionary<string, object>
{
    ["source"] = "wavu",
    ["characters"] = "Kazuya,Heihachi",
    ["requestDelay"] = 1,        // Быстрый парсинг
    ["characterDelay"] = 2,
    ["parseMoves"] = true,       // Парсить мувы
    ["useStaging"] = false,      // Прямо в базу
    ["timeout"] = 30,
    ["maxRetries"] = 3
};
```

## 🌐 **Примеры через API**

```json
// POST /api/framedata/parse
{
    "source": "wavu",
    "characterNames": ["Kazuya", "Heihachi"],
    "options": {
        "requestDelaySeconds": 1,
        "characterDelaySeconds": 2,
        "useStagingService": false,
        "parseMoves": true,
        "maxRetries": 3,
        "httpTimeoutSeconds": 30
    }
}
```

## 🤖 **Примеры через Telegram Bot**

```
/scrupframedata source:wavu characters:Kazuya,Heihachi parseMoves:false useStaging:false
/scrap source:tekkendocs requestDelay:5 characterDelay:10
/parse characters:Kazuya parseMoves:true useStaging:false
```

## ⚙️ **Логика выполнения**

1. **Если `parseMoves = true`**: Вызывается `ParseWithCustomOptions()` - полный парсинг с настройками
2. **Если `parseMoves = false`**: Вызывается `ParseCharactersOnly()` - только персонажи
3. **Если `useStaging = false`**: Изменения добавляются напрямую в базу данных
4. **Если `useStaging = true`**: Изменения проходят через `FramedataStagingService`

## 🔒 **Безопасность**

- Команда доступна только администраторам (`IsAdminCommand = true`)
- Поддерживает отмену через `CancellationToken`
- Выполняется асинхронно в фоновом потоке
- Возвращает подробное описание запущенных параметров
- Автоматическая валидация типов параметров

## 🆕 **Преимущества новой системы параметров**

1. **Автоматический парсинг**: Не нужно вручную разбирать строки параметров
2. **Валидация типов**: Автоматическое преобразование строк в нужные типы данных
3. **Значения по умолчанию**: Автоматическое заполнение необязательных параметров
4. **Документирование**: Описания параметров доступны для фронтенда
5. **Гибкость**: Поддержка как текстового ввода, так и программного вызова
