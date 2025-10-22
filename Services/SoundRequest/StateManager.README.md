# StateManager - Менеджер состояния плеера

## Описание

`StateManager` - это потокобезопасный менеджер для управления состоянием аудиоплеера с персистентностью в базе данных. Обеспечивает безопасную работу с `PlayerState` в многопоточной среде и автоматическое сохранение всех изменений в БД.

## Основные возможности

### 💾 Персистентность состояния

- **Автоматическое сохранение**: Каждое изменение состояния автоматически сохраняется в БД
- **Восстановление при запуске**: При старте приложения состояние загружается из БД
- **Устойчивость к перезапускам**: Состояние плеера сохраняется между перезапусками приложения

### 🔒 Потокобезопасность

- Использует `SemaphoreSlim` для асинхронной синхронизации
- Все операции чтения/записи защищены от race conditions
- Возвращает копии состояния для предотвращения внешних изменений

### 📢 События

```csharp
public event Func<PlayerState, Task>? StateChanged;
```

Событие вызывается при любом изменении состояния (если `notify = true`).

### 🔧 Базовые методы

#### Инициализация состояния

**ВАЖНО**: Метод `InitializeAsync()` должен вызываться один раз при старте приложения перед использованием StateManager.

```csharp
// Инициализация при старте приложения
await stateManager.InitializeAsync();
```

Этот метод:

- Загружает существующее состояние из БД
- Если состояния нет в БД - создает новое с дефолтными значениями
- Гарантирует потокобезопасную инициализацию (повторные вызовы игнорируются)

#### Получение состояния

```csharp
// Асинхронное получение
var state = await stateManager.GetStateAsync();

// Синхронное получение (для обратной совместимости)
var state = stateManager.GetState();
```

#### Универсальное обновление состояния

```csharp
await stateManager.UpdateStateAsync(state => 
{
    state.Volume = 50;
    state.IsMuted = false;
}, notifyStateChanged: true);
```

**Важно:**

- Изменения автоматически сохраняются в БД после обновления состояния
- Уведомление подписчиков происходит **после** сохранения в БД и освобождения блокировки для предотвращения deadlock
- При ошибке сохранения в БД - изменения остаются в памяти, ошибка логируется

### 🎵 Специализированные методы

#### Управление треками

```csharp
// Установить текущий трек
await stateManager.SetCurrentTrackAsync(trackInfo);

// Установить следующий трек
await stateManager.SetNextTrackAsync(nextTrackInfo);

// Начать воспроизведение
await stateManager.StartPlayingAsync(trackInfo);

// Остановить воспроизведение и очистить состояние
await stateManager.StopPlaybackAsync();
```

#### Управление воспроизведением

```csharp
// Пауза
await stateManager.SetPausedAsync(true);

// Отключение звука
await stateManager.SetMutedAsync(true);

// Остановка
await stateManager.SetStoppedAsync(true);

// Громкость (автоматически ограничивается 0-100)
await stateManager.SetVolumeAsync(75);
```

#### Уведомления

```csharp
// Уведомить подписчиков вручную
await stateManager.NotifyStateChangedAsync();
```

## Архитектура многопоточности

### Принцип работы блокировки

```
┌─────────────────────────────────────────┐
│  Поток 1: UpdateStateAsync              │
│  ┌────────────────────────────────┐     │
│  │ 1. await semaphore.WaitAsync() │     │
│  │ 2. Modify _currentState        │     │
│  │ 3. Create copy for notify      │ ◄───┼─── Критическая секция
│  │ 4. semaphore.Release()         │     │
│  └────────────────────────────────┘     │
│  5. Notify subscribers (unlock)         │
└─────────────────────────────────────────┘
```

### Пример многопоточного использования

```csharp
// Поток 1
await stateManager.SetCurrentTrackAsync(track1);

// Поток 2 (одновременно)
await stateManager.SetVolumeAsync(50);

// Результат: Оба изменения применены безопасно
// Порядок зависит от того, кто первым получил блокировку
```

## Жизненный цикл состояния

```
1. Запуск приложения
   ↓
2. InitializeAsync() - загрузка из БД или создание нового
   ↓
3. UpdateStateAsync() - изменение состояния
   ↓
4. SaveStateToDbAsync() - автоматическое сохранение в БД
   ↓
5. NotifyStateChanged() - уведомление подписчиков
   ↓
6. Перезапуск приложения → возврат к шагу 2
```

## Примеры использования

### Пример 1: Инициализация и подписка на события

```csharp
var stateManager = serviceProvider.GetRequiredService<StateManager>();

// ОБЯЗАТЕЛЬНО: Инициализация при старте
await stateManager.InitializeAsync();

stateManager.StateChanged += async (state) =>
{
    Console.WriteLine($"State changed: Volume={state.Volume}, State={state.State}");
    await signalRService.NotifyPlayerStateChangedAsync(state);
};

await stateManager.SetVolumeAsync(75);
// Output: State changed: Volume=75, State=Playing
// Состояние автоматически сохранено в БД
```

### Пример 2: Пакетное обновление

```csharp
// Несколько изменений в одной транзакции
await stateManager.UpdateStateAsync(state =>
{
    state.Volume = 80;
    state.IsMuted = false;
    state.IsPaused = false;
});
// Одно уведомление для всех изменений
```

### Пример 3: Обновление без уведомления

```csharp
// Полезно для внутренних изменений
await stateManager.SetVolumeAsync(50, notify: false);
await stateManager.SetMutedAsync(true, notify: false);

// Уведомить один раз после всех изменений
await stateManager.NotifyStateChangedAsync();
```

### Пример 4: Интеграция с плеером

```csharp
public class PlayerController : IPlayerController
{
    private readonly StateManager _stateManager;
    private readonly SignalRService _signalR;

    public async Task PlayAsync(BaseTrackInfo track, CancellationToken ct)
    {
        // Обновляем состояние
        await _stateManager.StartPlayingAsync(track);
        
        // Начинаем воспроизведение
        await ActualPlayAsync(track, ct);
    }

    public async Task PauseAsync(CancellationToken ct)
    {
        await _stateManager.SetPausedAsync(true);
        await ActualPauseAsync(ct);
    }
}
```

## Освобождение ресурсов

`StateManager` реализует `IDisposable` для корректного освобождения `SemaphoreSlim`:

```csharp
await using var stateManager = new StateManager();
// ... использование

// Автоматическое освобождение при выходе из scope
```

Или вручную:

```csharp
stateManager.Dispose();
```

## Преимущества

✅ **Персистентность** - автоматическое сохранение состояния в БД, восстановление при перезапуске  
✅ **Потокобезопасность** - защита от race conditions  
✅ **Изоляция** - возвращает копии для предотвращения внешних изменений  
✅ **Гибкость** - поддержка как общих, так и специализированных операций  
✅ **Производительность** - уведомления за пределами критической секции  
✅ **Удобство** - высокоуровневые методы для типичных операций  
✅ **Надежность** - состояние плеера сохраняется даже при неожиданном завершении работы  

## Внутренняя реализация

### Стратегия копирования

При каждом чтении создается новая копия `PlayerState`:

- Предотвращает внешние изменения внутреннего состояния
- Гарантирует консистентность данных
- Избегает проблем с concurrent modification

### Стратегия уведомлений

Уведомления происходят **после** освобождения блокировки:

- Предотвращает deadlock если обработчик пытается изменить состояние
- Улучшает производительность (блокировка держится минимальное время)
- Обработчики получают иммутабельную копию состояния

## Best Practices

1. **Всегда вызывайте InitializeAsync()** при старте приложения перед использованием StateManager
2. **Используйте специализированные методы** вместо прямого `UpdateStateAsync` где возможно
3. **Группируйте связанные изменения** в один вызов `UpdateStateAsync` - это сэкономит запросы к БД
4. **Отключайте уведомления** для промежуточных состояний, уведомляйте вручную в конце
5. **Не забывайте Dispose** - используйте `using` или `await using`
6. **В обработчиках событий избегайте** долгих операций или повторных вызовов StateManager
7. **Доверяйте автосохранению** - не нужно вручную сохранять состояние в БД

## Ограничения

⚠️ **Не храните прямые ссылки** на возвращенный `PlayerState` между вызовами - состояние может измениться  
⚠️ **Не модифицируйте** возвращенный `PlayerState` - это приведет к рассинхронизации  
⚠️ **Обработчики StateChanged** выполняются последовательно - избегайте тяжелых операций  
