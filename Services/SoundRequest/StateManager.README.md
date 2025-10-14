# StateManager - Менеджер состояния плеера

## Описание

`StateManager` - это потокобезопасный менеджер для управления состоянием аудиоплеера. Обеспечивает безопасную работу с `PlayerState` в многопоточной среде.

## Основные возможности

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

**Важно:** Уведомление подписчиков происходит **после** освобождения блокировки для предотвращения deadlock.

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

## Примеры использования

### Пример 1: Подписка на события

```csharp
var stateManager = new StateManager();

stateManager.StateChanged += async (state) =>
{
    Console.WriteLine($"State changed: Volume={state.Volume}, Paused={state.IsPaused}");
    await signalRService.NotifyPlayerStateChangedAsync(state);
};

await stateManager.SetVolumeAsync(75);
// Output: State changed: Volume=75, Paused=False
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

✅ **Потокобезопасность** - защита от race conditions  
✅ **Изоляция** - возвращает копии для предотвращения внешних изменений  
✅ **Гибкость** - поддержка как общих, так и специализированных операций  
✅ **Производительность** - уведомления за пределами критической секции  
✅ **Удобство** - высокоуровневые методы для типичных операций  

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

1. **Используйте специализированные методы** вместо прямого `UpdateStateAsync` где возможно
2. **Группируйте связанные изменения** в один вызов `UpdateStateAsync`
3. **Отключайте уведомления** для промежуточных состояний, уведомляйте вручную в конце
4. **Не забывайте Dispose** - используйте `using` или `await using`
5. **В обработчиках событий избегайте** долгих операций или повторных вызовов StateManager

## Ограничения

⚠️ **Не храните прямые ссылки** на возвращенный `PlayerState` между вызовами - состояние может измениться  
⚠️ **Не модифицируйте** возвращенный `PlayerState` - это приведет к рассинхронизации  
⚠️ **Обработчики StateChanged** выполняются последовательно - избегайте тяжелых операций  
