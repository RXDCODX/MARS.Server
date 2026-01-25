# FreeTTS Synthesizer Service - Документация

## Обзор

`FreeTTS Synthesizer` - это сервис онлайн синтеза речи, использующий API сервиса [FreeTTS](https://freetts.ru/). Сервис предоставляет альтернативу локальному синтезу речи (Synthesizer для Windows) и доступен на всех платформах.

## Структура

### Основные компоненты

```
Services/Twitch/Synthesizer/
├── TextProcessing/
│   └── TextNormalizationService.cs         # Нормализация текста
├── FreeTts/
│   ├── Models/
│   │   └── FreeTtsModels.cs               # DTO модели API
│   ├── FreeTtsHttpClient.cs               # HTTP клиент для API
│   └── FreeTtsHealthCheckService.cs       # Проверка здоровья сервиса
├── FreeTtsSynthesizerService.cs           # Основной сервис синтеза
├── FreeTtsVoicer.cs                       # Реализация IVoicer
└── VoicerFactory.cs                       # Фабрика для создания voicer'ов
```

## Компоненты

### 1. TextNormalizationService (`TextProcessing/TextNormalizationService.cs`)

Сервис для нормализации текста перед синтезом:

```csharp
public interface ITextNormalizationService
{
    /// Нормализует текст, удаляя или заменяя не-кириллические символы
    string Normalize(string text, bool replaceMode = false);

    /// Проверяет наличие не-кириллических символов
    bool HasNonCyrillicCharacters(string text);
}
```

**Режимы нормализации:**
- **replaceMode = false**: Удаление не-кириллических символов
- **replaceMode = true**: Замена на похожие кириллические символы (транслитерация)

**Пример:**
```csharp
var normalizer = serviceProvider.GetRequiredService<ITextNormalizationService>();

// Удаление неподдерживаемых символов
var text1 = normalizer.Normalize("Hello Привет!", replaceMode: false); // "Привет"

// Замена на кириллические эквиваленты
var text2 = normalizer.Normalize("Hello Привет!", replaceMode: true); // "Хеллo Привет"
```

### 2. FreeTtsHttpClient (`FreeTts/FreeTtsHttpClient.cs`)

Низкоуровневый HTTP клиент для работы с API FreeTTS:

```csharp
public interface IFreeTtsHttpClient
{
    Task<FreeTtsListResponse?> GetVoicesAsync(CancellationToken cancellationToken = default);
    Task<FreeTtsSynthesisResponse?> SynthesizeAsync(
        string text,
        string voiceId,
        CancellationToken cancellationToken = default
    );
    Task<FreeTtsHistoryResponse?> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    void GenerateRandomUid();
}
```

**Особенности:**
- Автоматическое генерирование случайного UID (уникального идентификатора браузера)
- Установка UID в cookies для каждого запроса
- Обработка ошибок и логирование
- Таймаут на проверку доступности (10 секунд)

### 3. FreeTtsHealthCheckService (`FreeTts/FreeTtsHealthCheckService.cs`)

Сервис проверки здоровья и кеширования голосов:

```csharp
public interface IFreeTtsHealthCheckService
{
    Task<FreeTtsHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default);
    FreeTtsHealthResponse GetLastCheckResult();
    Task<List<FreeTtsVoice>> GetCachedVoicesAsync(CancellationToken cancellationToken = default);
}
```

**Особенности:**
- Проверка доступности сервиса
- Кеширование списка голосов (60 минут)
- Сохранение последнего результата проверки
- Автоматическое обновление кеша при успешной проверке

### 4. FreeTtsSynthesizerService (`FreeTts/FreeTtsSynthesizerService.cs`)

Основной сервис синтеза речи:

```csharp
public interface IFreeTtsSynthesizerService
{
    Task<string?> SynthesizeAsync(string text, string voiceId, CancellationToken cancellationToken = default);
    Task<byte[]?> SynthesizeAndGetAudioAsync(string text, string voiceId, CancellationToken cancellationToken = default);
    Task<List<FreeTtsVoice>> GetAvailableVoicesAsync(CancellationToken cancellationToken = default);
    Task<FreeTtsVoice?> FindVoiceByNameAsync(string voiceName, CancellationToken cancellationToken = default);
    Task<List<FreeTtsVoice>> GetVoicesByLanguageAsync(string languageCode, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
```

**Возвращаемые значения:**
- `SynthesizeAsync`: URL на аудиофайл (или null при ошибке)
- `SynthesizeAndGetAudioAsync`: Байты аудиофайла (или null при ошибке)

### 5. FreeTtsVoicer (`FreeTtsVoicer.cs`)

Реализация интерфейса `IVoicer` для использования в существующей архитектуре:

```csharp
public class FreeTtsVoicer : IVoicer
{
    // Все методы IVoicer
    public bool IsActive { get; set; }
    public int GetVolume();
    public void ChangeVolume(int volume);
    public Task Sound(MessageToSynthezid message);
    public Task Stop();
    public Task Block();
    public Task Unblock();
    // ... и другие методы
}
```

## Регистрация в DI контейнере

Добавьте следующее в `Startup.cs` или `Program.cs`:

```csharp
// Регистрация всех сервисов FreeTTS
services.AddScoped<ITextNormalizationService, TextNormalizationService>();

// Регистрация HTTP клиента
services.AddHttpClient<IFreeTtsHttpClient, FreeTtsHttpClient>();

services.AddScoped<IFreeTtsHealthCheckService, FreeTtsHealthCheckService>();
services.AddScoped<IFreeTtsSynthesizerService, FreeTtsSynthesizerService>();

// FreeTtsVoicer можно создать через фабрику
services.AddScoped(serviceProvider =>
{
    var synthesizerService = serviceProvider.GetRequiredService<IFreeTtsSynthesizerService>();
    var voiceRepository = serviceProvider.GetRequiredService<ITtsVoiceRepository>();
    var logger = serviceProvider.GetRequiredService<ILogger<IVoicer>>();
    
    return new FreeTtsVoicer(synthesizerService, voiceRepository, logger);
});
```

## Использование

### Пример 1: Синтез текста и получение URL

```csharp
var synthesizer = serviceProvider.GetRequiredService<IFreeTtsSynthesizerService>();

// Получить доступные голоса
var voices = await synthesizer.GetAvailableVoicesAsync();

// Найти голос по названию
var voice = await synthesizer.FindVoiceByNameAsync("Эмиль");

if (voice != null)
{
    // Синтезировать текст
    var audioUrl = await synthesizer.SynthesizeAsync(
        "Привет, мир!",
        voice.Id
    );

    if (!string.IsNullOrEmpty(audioUrl))
    {
        Console.WriteLine($"Аудио доступно по ссылке: {audioUrl}");
    }
}
```

### Пример 2: Синтез и скачивание аудио

```csharp
var synthesizer = serviceProvider.GetRequiredService<IFreeTtsSynthesizerService>();
var voices = await synthesizer.GetVoicesByLanguageAsync("ru-RU");

if (voices.Any())
{
    var audioBytes = await synthesizer.SynthesizeAndGetAudioAsync(
        "Это пример синтеза",
        voices.First().Id
    );

    if (audioBytes != null)
    {
        await File.WriteAllBytesAsync("audio.mp3", audioBytes);
    }
}
```

### Пример 3: Проверка доступности сервиса

```csharp
var healthCheck = serviceProvider.GetRequiredService<IFreeTtsHealthCheckService>();

var health = await healthCheck.CheckHealthAsync();

if (health.IsAvailable)
{
    Console.WriteLine($"Сервис доступен (проверено в {health.CheckedAt:G})");
}
else
{
    Console.WriteLine($"Сервис недоступен: {health.Message}");
}
```

### Пример 4: Использование в качестве IVoicer

```csharp
var voicer = serviceProvider.GetRequiredService<FreeTtsVoicer>();

// Синтезировать и проиграть сообщение
var message = new MessageToSynthezid
{
    Message = "Привет, это тестовое сообщение",
    Name = "Эмиль"  // Используется для поиска голоса
};

await voicer.Sound(message);

// Получить доступные голоса
var installedVoices = await voicer.GetInstalledVoicesAsync();
Console.WriteLine($"Доступно голосов: {installedVoices.Count}");

// Заблокировать конкретный голос
await voicer.RefreshBlockedVoicesAsync();
```

## Доступные голоса

Сервис FreeTTS предоставляет голоса на множестве языков:
- **Русский (ru-RU)**: Ермилов, Маргарита, Евгений, Николай и многие другие
- **Украинский (uk-UA)**: Мила, Захар, Евгений
- **Английский (en-US, en-GB, en-AU)**: Грейс, Мэтью, Уинстон и другие
- **И многие другие языки**: французский, немецкий, испанский, японский, китайский и т.д.

## Особенности

### Нормализация текста
- Автоматическая замена или удаление не-кириллических символов
- Удаление лишних пробелов
- Сохранение пунктуации (если включена)

### Управление куками
- Автоматическое генерирование случайного UID при инициализации
- UID устанавливается в формате: `uid=a2b4061f78a4baabab746b09a5f99148`
- Регенерация UID доступна через `GenerateRandomUid()`

### Кеширование
- Список голосов кешируется на 60 минут
- Кеш автоматически обновляется при проверке здоровья
- Манипуляция кешем: `RefreshVoicesAsync()`

### Обработка ошибок
- Все методы предоставляют graceful degradation
- Детальное логирование всех операций
- Таймауты на все HTTP запросы

## Модели данных

### FreeTtsVoice
```csharp
public class FreeTtsVoice
{
    public string Id { get; set; }          // Уникальный идентификатор голоса
    public string Lang { get; set; }        // Языковой код (en-US, ru-RU и т.д.)
    public string Name { get; set; }        // Отображаемое имя голоса
    public string Sex { get; set; }         // Пол: "m" (мужской) или "f" (женский)
}
```

### FreeTtsHealthResponse
```csharp
public class FreeTtsHealthResponse
{
    public bool IsAvailable { get; set; }        // Доступен ли сервис
    public string Message { get; set; }          // Сообщение о статусе
    public DateTime CheckedAt { get; set; }      // Время проверки
}
```

## Обработка ошибок

Все методы возвращают `null` или `false` при ошибках:

```csharp
var audioUrl = await synthesizer.SynthesizeAsync("text", "voice-id");

if (audioUrl == null)
{
    // Сервис недоступен или произошла ошибка
    _logger.LogError("Synthesis failed");
}
```

Детали ошибок логируются в приложение через `ILogger`.

## Требования

- .NET Standard 2.0+ / .NET 9+
- HTTP клиент (HttpClient)
- Доступ в интернет для соединения с `freetts.ru`
- Опционально: реализация `ITtsVoiceRepository` для управления заблокированными голосами

## Лицензия и правовые уведомления

Этот сервис использует API [FreeTTS](https://freetts.ru/). Убедитесь, что использование соответствует их условиям обслуживания.
