# FreeTTS Synthesizer - Архитектурная диаграмма

## Общая архитектура

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Application Layer                          │
│  (Controllers, Commands, Services, IVoicer consumers)              │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    FreeTtsVoicer (IVoicer)                         │
│  - Sound(message)                                                   │
│  - GetInstalledVoicesAsync()                                       │
│  - GetLinkedVoicesAsync()                                          │
│  - Volume management                                                │
│  - Block/Unblock                                                    │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│              IFreeTtsSynthesizerService                             │
│  - SynthesizeAsync(text, voiceId)                                  │
│  - SynthesizeAndGetAudioAsync(text, voiceId)                       │
│  - GetAvailableVoicesAsync()                                       │
│  - FindVoiceByNameAsync(name)                                      │
│  - GetVoicesByLanguageAsync(lang)                                  │
│  - IsAvailableAsync()                                              │
└──────────────────┬──────────────────────────────────────────────────┘
                   │
        ┌──────────┴──────────┐
        │                     │
        ▼                     ▼
┌──────────────────┐  ┌──────────────────────────────┐
│Text Normalization│  │IFreeTtsHealthCheckService   │
│Service           │  │ - CheckHealthAsync()        │
│                  │  │ - GetCachedVoicesAsync()    │
│- Normalize()     │  │ - GetLastCheckResult()      │
│- Remove/Replace  │  └──────────────┬───────────────┘
│  non-Cyrillic    │                 │
└──────────────────┘                 ▼
                          ┌──────────────────────┐
                          │IFreeTtsHttpClient    │
                          │ - GetVoicesAsync()   │
                          │ - SynthesizeAsync()  │
                          │ - GetHistoryAsync()  │
                          │ - IsAvailableAsync() │
                          │ - GenerateRandomUid()│
                          └──────────┬───────────┘
                                     │
                                     ▼
                         ┌──────────────────────┐
                         │ FreeTTS API         │
                         │ https://freetts.ru  │
                         │ /api/list           │
                         │ /api/synthesis      │
                         │ /api/history        │
                         └─────────────────────┘
```

## Слои приложения

### Уровень 1: Приложение (Application Layer)
- Controllers
- Commands
- Services (другие сервисы приложения)
- Что-угодно, использующее `IVoicer`

### Уровень 2: Voicer Layer
```
VoicerFactory
    ↓
FreeTtsVoicer (implements IVoicer)
    ├── SyntheziaVoicer (Windows only)
    └── NullVoicer (fallback)
```

### Уровень 3: Synthesis Service Layer
```
IFreeTtsSynthesizerService
├── Синтез текста
├── Управление голосами
└── Проверка доступности
```

### Уровень 4: Support Services
```
┌─────────────────────────────┐
│ Health Check Service        │
├─────────────────────────────┤
│ - Проверка доступности      │
│ - Кеширование голосов       │
│ - Последний результат       │
└──────────────┬──────────────┘
               │
┌──────────────┴──────────────┐
│ Text Normalization Service  │
├─────────────────────────────┤
│ - Нормализация текста       │
│ - Удаление символов         │
│ - Транслитерация            │
└─────────────────────────────┘
```

### Уровень 5: HTTP Client Layer
```
IFreeTtsHttpClient
├── Управление куками (UID)
├── HTTP запросы
└── Обработка JSON
```

### Уровень 6: External API
```
FreeTTS API (https://freetts.ru/)
├── /api/list       - Список голосов
├── /api/synthesis  - Синтез текста
└── /api/history    - История синтезов
```

## Диаграмма взаимодействия компонентов

```
┌─────────────────┐
│ Application     │
└────────┬────────┘
         │ uses
         ▼
    ┌─────────────────────────┐
    │   VoicerFactory         │
    │   ↓                     │
    │ CreateVoicer()          │
    │ CreateFreeTtsVoicer()   │
    └────────┬────────────────┘
             │ creates
             ▼
    ┌─────────────────────────┐
    │   FreeTtsVoicer         │
    │   (implements IVoicer)  │
    │                         │
    │   - Sound()             │
    │   - GetVolume()         │
    │   - Block/Unblock       │
    └────────┬────────────────┘
             │ uses
             ▼
    ┌─────────────────────────┐
    │ FreeTtsSynthesizer      │
    │ Service                 │
    │                         │
    │ - Synthesize()          │
    │ - FindVoiceByName()     │
    │ - GetAvailableVoices()  │
    └──┬──────────────────┬───┘
       │ uses             │ uses
       ▼                  ▼
    ┌────────────────┐ ┌──────────────────┐
    │ Health Check   │ │ Text             │
    │ Service        │ │ Normalization    │
    │                │ │ Service          │
    │ - Caching      │ │                  │
    │ - Validation   │ │ - Normalize()    │
    └────────┬───────┘ │ - Check chars    │
             │         └──────────────────┘
             │ uses
             ▼
    ┌─────────────────────────┐
    │   FreeTtsHttpClient     │
    │                         │
    │ - GetVoices()           │
    │ - Synthesize()          │
    │ - GetHistory()          │
    │ - GenerateUID()         │
    └────────┬────────────────┘
             │ makes HTTP calls
             ▼
    ┌─────────────────────────┐
    │   FreeTTS API           │
    │   (freetts.ru)          │
    └─────────────────────────┘
```

## Последовательность операций: Синтез текста

```
User
  │
  └─> FreeTtsVoicer.Sound(message)
       │
       └─> FindVoiceByName(message.Name)
            │
            └─> IFreeTtsSynthesizerService.FindVoiceByNameAsync()
                 │
                 └─> GetAvailableVoicesAsync()
                      │
                      └─> IFreeTtsHealthCheckService.GetCachedVoicesAsync()
                           │ (cache hit or miss)
                           └─> IFreeTtsHttpClient.GetVoicesAsync()
                                │
                                └─> FreeTTS API /api/list
                                     │
                                     └─> HTTP GET with UID cookie
                                          └─> Response: 512 voices
       │
       └─> Normalize text
            │
            └─> ITextNormalizationService.Normalize()
                 │
                 └─> Remove/Replace non-Cyrillic
       │
       └─> IFreeTtsSynthesizerService.SynthesizeAsync(text, voiceId)
            │
            └─> IFreeTtsHttpClient.SynthesizeAsync()
                 │
                 ├─> HTTP POST /api/synthesis
                 │    └─> { text, voiceId, ext: "mp3" }
                 │         └─> Response: { status: "pending" }
                 │
                 └─> Wait + IFreeTtsHttpClient.GetHistoryAsync()
                      │
                      └─> HTTP GET /api/history
                           │
                           └─> Response: { data: [...] }
                                └─> Extract audioUrl from latest item
       │
       └─> Return audioUrl to User
            │
            └─> audioUrl can be:
                 - Played in browser
                 - Downloaded
                 - Streamed
```

## Последовательность: Проверка доступности

```
User
  │
  └─> IFreeTtsHealthCheckService.CheckHealthAsync()
       │
       ├─> IFreeTtsHttpClient.IsAvailableAsync()
       │    │
       │    └─> HTTP GET /api/list (with 10s timeout)
       │         │
       │         └─> Success? → true : false
       │
       ├─> If true:
       │    └─> RefreshVoicesCache()
       │         │
       │         └─> GetVoices() → Store + Update timestamp
       │
       └─> Return FreeTtsHealthResponse
            │
            └─> { IsAvailable, Message, CheckedAt }
```

## Структура данных

```
MessageToSynthezid
├── Message: string       (текст для синтеза)
├── Name: string          (имя голоса)
├── CreationDateTime      (время создания)
└── Guid: Guid           (уникальный идентификатор)

FreeTtsVoice
├── Id: string           (уникальный ID голоса)
├── Lang: string         (код языка: ru-RU, en-US и т.д.)
├── Name: string         (отображаемое имя: Эмиль, Грейс и т.д.)
└── Sex: string          (пол: m/f)

FreeTtsHealthResponse
├── IsAvailable: bool    (доступен ли сервис)
├── Message: string      (сообщение о статусе)
└── CheckedAt: DateTime  (время проверки)

FreeTtsSynthesisRequest
├── Text: string         (текст для синтеза)
├── VoiceId: string      (ID голоса)
└── Ext: string          (формат: mp3)

FreeTtsSynthesisResponse
├── Status: string       (pending/success/error)
├── Message: string      (сообщение)
└── Data: bool          (успех операции)
```

## Управление состоянием (State Management)

```
FreeTtsVoicer
├── IsActive: bool
│    ├─ true: может синтезировать
│    └─ false: заблокирован
│
├── _volume: int (0-100)
│    └─ влияет на громкость
│
├── _blockedVoices: HashSet<string>
│    └─ голоса, которые заблокированы
│
└── _voiceCache: Dictionary<string, FreeTtsVoice>
     └─ кеш голосов для быстрого поиска

IFreeTtsHealthCheckService
├── _lastCheckResult: FreeTtsHealthResponse
│    └─ результат последней проверки
│
└── _cachedVoices: List<FreeTtsVoice>
     ├─ кеш голосов
     └─ _voicesCacheTime: DateTime (60 min TTL)

IFreeTtsHttpClient
├── _currentUid: string
│    └─ текущий UID для cookies
│
└── _httpClient.DefaultRequestHeaders["Cookie"]
     └─ содержит текущий UID
```

## Error Handling Flow

```
API Call
  │
  └─> Try
       │
       ├─> HTTP Request
       │    │
       │    ├─> Success (200-299)
       │         └─> Parse JSON
       │              └─> Return object
       │
       └─> Catch Exception
            │
            ├─> Log error
            │    └─ _logger.LogError(ex, "message")
            │
            └─> Return null / false
                 │
                 └─ Graceful degradation
```

## DI Container Setup

```
services.AddFreeTtsSynthesizer()
  ├─ AddScoped<ITextNormalizationService, TextNormalizationService>()
  ├─ AddHttpClient<IFreeTtsHttpClient, FreeTtsHttpClient>()
  ├─ AddScoped<IFreeTtsHealthCheckService, FreeTtsHealthCheckService>()
  └─ AddScoped<IFreeTtsSynthesizerService, FreeTtsSynthesizerService>()

services.AddFreeTtsVoicer()
  ├─ AddFreeTtsSynthesizer() [implicitly]
  └─ AddScoped<FreeTtsVoicer>()
       │
       └─ Constructor injection:
            ├─ IFreeTtsSynthesizerService
            ├─ ITtsVoiceRepository
            └─ ILogger<IVoicer>
```

## Сравнение с Synthesizer (Windows TTS)

```
┌─────────────────────┬──────────────────────┐
│    Synthesizer      │   FreeTts            │
├─────────────────────┼──────────────────────┤
│ Platform: Windows   │ Platform: All        │
│ Voices: 3-10        │ Voices: 100+         │
│ Languages: 5-10     │ Languages: 50+       │
│ Local processing    │ Online API           │
│ No network needed   │ Requires internet    │
│ Same IVoicer        │ Same IVoicer         │
│ Can switch via      │ Can switch via       │
│ VoicerFactory       │ VoicerFactory        │
└─────────────────────┴──────────────────────┘
```

---

**Диаграмма отражает архитектуру версии от 25.01.2026**
