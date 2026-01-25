# FreeTTS Synthesizer Service - Быстрый старт

## Что было создано

Полнофункциональный сервис синтеза речи на основе онлайн API FreeTTS с поддержкой 100+ голосов на 50+ языках.

## Структура проекта

```
Services/Twitch/Synthesizer/
├── TextProcessing/
│   └── TextNormalizationService.cs              # Нормализация текста (удаление/замена не-кириллических символов)
│
├── FreeTts/
│   ├── Models/
│   │   └── FreeTtsModels.cs                    # DTO для API (голоса, языки, запросы/ответы)
│   │
│   ├── FreeTtsHttpClient.cs                    # HTTP клиент с поддержкой UID cookies
│   ├── FreeTtsHealthCheckService.cs            # Проверка доступности и кеширование голосов
│   ├── FreeTtsSynthesizerExample.cs            # 9 примеров использования
│   └── FreeTtsSynthesizerServiceCollectionExtensions.cs  # Регистрация в DI
│
├── FreeTtsVoicer.cs                            # Реализация IVoicer интерфейса
├── VoicerFactory.cs                            # Обновлена для поддержки FreeTTS
├── FreeTtsSynthesizerService.cs                # Основной сервис синтеза
└── FreeTTS_README.md                           # Подробная документация
```

## Ключевые особенности

### ✅ Нормализация текста
- **Удаление** не-кириллических символов (режим по умолчанию)
- **Замена** на похожие кириллические символы (транслитерация)
- Автоматическая очистка от лишних пробелов

### ✅ Управление куками
- Автоматическое генерирование **случайного UID** при инициализации
- Формат UID: `a2b4061f78a4baabab746b09a5f99148` (32 символа hex)
- Отправка UID в cookies для каждого запроса
- Возможность регенерации через `GenerateRandomUid()`

### ✅ Проверка доступности
- Асинхронная проверка с таймаутом (10 сек)
- Кеширование списка голосов на 60 минут
- Автоматическое обновление кеша при проверке здоровья
- Сохранение результата последней проверки

### ✅ Управление голосами
- Получение полного списка голосов (100+ голосов)
- Фильтрация по языку (более 50 языков)
- Поиск голоса по названию (с partial matching)
- Поддержка блокирования голосов

## Быстрый старт

### 1. Регистрация в DI контейнере

В `Program.cs` или `Startup.cs`:

```csharp
// Вариант 1: Только сервисы синтеза
builder.Services.AddFreeTtsSynthesizer();

// Вариант 2: С FreeTtsVoicer (реализация IVoicer)
builder.Services.AddFreeTtsVoicer();
```

### 2. Использование в коде

```csharp
// Инъекция через конструктор
public class MyService
{
    private readonly IFreeTtsSynthesizerService _synthesizer;
    
    public MyService(IFreeTtsSynthesizerService synthesizer)
    {
        _synthesizer = synthesizer;
    }
    
    public async Task SynthesizeText()
    {
        // Получить голос
        var voice = await _synthesizer.FindVoiceByNameAsync("Эмиль");
        
        // Синтезировать текст
        var audioUrl = await _synthesizer.SynthesizeAsync(
            "Привет, мир!",
            voice.Id
        );
        
        if (audioUrl != null)
        {
            // Использовать audioUrl для воспроизведения или скачивания
        }
    }
}
```

### 3. Проверка доступности сервиса

```csharp
var healthCheck = serviceProvider.GetRequiredService<IFreeTtsHealthCheckService>();
var health = await healthCheck.CheckHealthAsync();

if (health.IsAvailable)
{
    Console.WriteLine("FreeTTS доступен");
}
```

## Примеры использования

### Синтез текста с автоматической нормализацией

```csharp
var synthesizer = serviceProvider.GetRequiredService<IFreeTtsSynthesizerService>();
var textNormalizer = serviceProvider.GetRequiredService<ITextNormalizationService>();

// Текст с английскими буквами и спецсимволами
var mixedText = "Hello Привет 123!";

// Нормализовать (удалит "Hello 123!")
var normalized = textNormalizer.Normalize(mixedText);
// Result: "Привет!"

// Синтезировать нормализованный текст
var audioUrl = await synthesizer.SynthesizeAsync(
    normalized,
    "NG6FIoMMe4L1"  // ID голоса Ермилова
);
```

### Получение всех русских голосов

```csharp
var voices = await synthesizer.GetVoicesByLanguageAsync("ru-RU");

foreach (var voice in voices)
{
    Console.WriteLine($"{voice.Name} ({voice.Sex})");
}
```

### Синтез и скачивание аудиофайла

```csharp
var audioBytes = await synthesizer.SynthesizeAndGetAudioAsync(
    "Текст для синтеза",
    "tOvhtxQAgtH"  // ID голоса Маргариты
);

if (audioBytes != null)
{
    await File.WriteAllBytesAsync("audio.mp3", audioBytes);
}
```

### Использование как IVoicer

```csharp
var voicer = serviceProvider.GetRequiredService<FreeTtsVoicer>();

var message = new MessageToSynthezid
{
    Message = "Тестовое сообщение",
    Name = "Анастасия"  // Голос будет найден по имени
};

await voicer.Sound(message);
```

## API и интерфейсы

### IFreeTtsSynthesizerService
```csharp
Task<string?> SynthesizeAsync(string text, string voiceId);
Task<byte[]?> SynthesizeAndGetAudioAsync(string text, string voiceId);
Task<List<FreeTtsVoice>> GetAvailableVoicesAsync();
Task<FreeTtsVoice?> FindVoiceByNameAsync(string voiceName);
Task<List<FreeTtsVoice>> GetVoicesByLanguageAsync(string languageCode);
Task<bool> IsAvailableAsync();
```

### ITextNormalizationService
```csharp
string Normalize(string text, bool replaceMode = false);
bool HasNonCyrillicCharacters(string text);
```

### IFreeTtsHealthCheckService
```csharp
Task<FreeTtsHealthResponse> CheckHealthAsync();
FreeTtsHealthResponse GetLastCheckResult();
Task<List<FreeTtsVoice>> GetCachedVoicesAsync();
```

## Доступные голоса

### Русский (ru-RU)
Ермилов, Маргарита, Евгений, Николай, Анатолий, Константин, Захар, Оксана, Александра, Татьяна, Анастасия, Жанна, Анжелика, Никита, Наталья, Абрамова, Смоки, Светлана, Дмитрий, Эдуард, Дина, Роберт, Лидия, Герман, Евдокия, Эмиль, Валентина, Виталий, Юра, Агафья

### Английский
- en-US: Грейс, Мэтью, Остин, Эрл, Уилфред, Оливия, Анна, Эндрю
- en-GB: Уинстон, Трэвис, Эмма, Либби, Мейзи, Райан, Соня, Томас
- en-AU: Бренда, Саймон, Наташа, Уильям, Шарлотта

### Другие языки
Украинский, Турецкий, Французский, Немецкий, Испанский, Португальский, Итальянский, Японский, Китайский, Корейский, и 40+ других языков

## Логирование

Все события логируются через `ILogger<T>`:

```
[Information] Generated new UID for FreeTTS: a2b4061f78a4baabab746b09a5f99148
[Information] FreeTTS service health check passed
[Information] FreeTTS voices cache updated with 512 voices
[Information] Synthesizing text: 'Привет' with voice: NG6FIoMMe4L1
[Information] Synthesis successful, audio URL: https://freetts.ru/api/audio/...
```

## Обработка ошибок

Все методы возвращают null/false при ошибках:

```csharp
var audioUrl = await synthesizer.SynthesizeAsync("text", "voice-id");

if (audioUrl == null)
{
    // Ошибка - детали в логах
    _logger.LogError("Synthesis failed");
}
```

## Производительность

- **Кеширование голосов**: 60 минут TTL
- **Таймаут на проверку**: 10 секунд
- **Таймаут на синтез**: 30 секунд (HTTP клиент)
- **Асинхронные операции**: Полностью async/await

## Требования

- .NET Standard 2.0+ / .NET 9+
- HttpClient в DI контейнере
- Доступ в интернет (freetts.ru)
- Опционально: ITtsVoiceRepository для управления блокировками

## Файлы для изучения

1. **FreeTTS_README.md** - Подробная документация
2. **FreeTtsSynthesizerExample.cs** - 9 примеров использования
3. **FreeTtsSynthesizerServiceCollectionExtensions.cs** - Регистрация в DI

## Дальнейшее развитие

Возможные улучшения:
- Полиморфный выбор TTS сервиса (Synthezia + FreeTTS)
- Кеширование синтезированного аудио
- Преобразование аудио (громкость, скорость)
- Поддержка SSML для более сложного синтеза
- Метрики производительности и аналитика использования

---

**Статус**: ✅ Полностью реализовано и протестировано на компиляцию
