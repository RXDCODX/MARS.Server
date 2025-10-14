using MARS.Server.Services.SoundRequest.Entities;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Менеджер состояния плеера с поддержкой многопоточности
/// </summary>
public class StateManager : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly PlayerState _currentState = new()
    {
        Id = Guid.NewGuid(),
        Volume = 100,
        IsPaused = false,
        IsMuted = false,
        IsStoped = true,
    };
    private bool _disposed;

    /// <summary>
    /// Событие изменения состояния плеера
    /// </summary>
    public event Func<PlayerState, Task>? StateChanged;

    /// <summary>
    /// Получить текущее состояние плеера (потокобезопасно)
    /// </summary>
    public async Task<PlayerState> GetStateAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            // Возвращаем копию состояния
            return new PlayerState
            {
                Id = _currentState.Id,
                CurrentTrack = _currentState.CurrentTrack,
                NextTrack = _currentState.NextTrack,
                CurrentTrackDuration = _currentState.CurrentTrackDuration,
                IsPaused = _currentState.IsPaused,
                IsMuted = _currentState.IsMuted,
                IsStoped = _currentState.IsStoped,
                Volume = _currentState.Volume,
                CurrentTrackRequestedBy = _currentState.CurrentTrackRequestedBy,
                CurrentTrackRequestedByDisplayName = _currentState.CurrentTrackRequestedByDisplayName,
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Получить текущее состояние плеера синхронно (для обратной совместимости)
    /// </summary>
    public PlayerState GetState()
    {
        _semaphore.Wait();
        try
        {
            return new PlayerState
            {
                Id = _currentState.Id,
                CurrentTrack = _currentState.CurrentTrack,
                NextTrack = _currentState.NextTrack,
                CurrentTrackDuration = _currentState.CurrentTrackDuration,
                IsPaused = _currentState.IsPaused,
                IsMuted = _currentState.IsMuted,
                IsStoped = _currentState.IsStoped,
                Volume = _currentState.Volume,
                CurrentTrackRequestedBy = _currentState.CurrentTrackRequestedBy,
                CurrentTrackRequestedByDisplayName = _currentState.CurrentTrackRequestedByDisplayName,
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Обновить состояние плеера (потокобезопасно)
    /// </summary>
    /// <param name="updateAction">Действие для обновления состояния</param>
    /// <param name="notifyStateChanged">Уведомлять ли подписчиков об изменении</param>
    public async Task UpdateStateAsync(
        Action<PlayerState> updateAction,
        bool notifyStateChanged = true
    )
    {
        PlayerState? stateToNotify = null;

        await _semaphore.WaitAsync();
        try
        {
            updateAction(_currentState);

            if (notifyStateChanged)
            {
                stateToNotify = new PlayerState
                {
                    Id = _currentState.Id,
                    CurrentTrack = _currentState.CurrentTrack,
                    NextTrack = _currentState.NextTrack,
                    CurrentTrackDuration = _currentState.CurrentTrackDuration,
                    IsPaused = _currentState.IsPaused,
                    IsMuted = _currentState.IsMuted,
                    IsStoped = _currentState.IsStoped,
                    Volume = _currentState.Volume,
                    CurrentTrackRequestedBy = _currentState.CurrentTrackRequestedBy,
                    CurrentTrackRequestedByDisplayName = _currentState.CurrentTrackRequestedByDisplayName,
                };
            }
        }
        finally
        {
            _semaphore.Release();
        }

        // Уведомляем подписчиков за пределами блокировки
        if (stateToNotify != null && StateChanged != null)
        {
            await StateChanged.Invoke(stateToNotify);
        }
    }

    /// <summary>
    /// Установить текущий трек
    /// </summary>
    public async Task SetCurrentTrackAsync(BaseTrackInfo? track, bool notify = true)
    {
        await UpdateStateAsync(
            state =>
            {
                state.CurrentTrack = track;
                state.CurrentTrackDuration = track?.Duration;
                state.IsStoped = track == null;
            },
            notify
        );
    }

    /// <summary>
    /// Установить текущий трек с информацией о пользователе, заказавшем трек
    /// </summary>
    public async Task SetCurrentTrackAsync(
        BaseTrackInfo? track,
        string? requestedBy,
        string? requestedByDisplayName,
        bool notify = true
    )
    {
        await UpdateStateAsync(
            state =>
            {
                state.CurrentTrack = track;
                state.CurrentTrackDuration = track?.Duration;
                state.IsStoped = track == null;
                state.CurrentTrackRequestedBy = requestedBy;
                state.CurrentTrackRequestedByDisplayName = requestedByDisplayName;
            },
            notify
        );
    }

    /// <summary>
    /// Установить следующий трек
    /// </summary>
    public async Task SetNextTrackAsync(BaseTrackInfo? track, bool notify = true)
    {
        await UpdateStateAsync(state => state.NextTrack = track, notify);
    }

    /// <summary>
    /// Установить состояние паузы
    /// </summary>
    public async Task SetPausedAsync(bool isPaused, bool notify = true)
    {
        await UpdateStateAsync(state => state.IsPaused = isPaused, notify);
    }

    /// <summary>
    /// Установить состояние отключения звука
    /// </summary>
    public async Task SetMutedAsync(bool isMuted, bool notify = true)
    {
        await UpdateStateAsync(state => state.IsMuted = isMuted, notify);
    }

    /// <summary>
    /// Установить состояние остановки
    /// </summary>
    public async Task SetStoppedAsync(bool isStopped, bool notify = true)
    {
        await UpdateStateAsync(
            state =>
            {
                state.IsStoped = isStopped;
                if (isStopped)
                {
                    state.CurrentTrack = null;
                    state.CurrentTrackDuration = null;
                }
            },
            notify
        );
    }

    /// <summary>
    /// Установить громкость
    /// </summary>
    public async Task SetVolumeAsync(int volume, bool notify = true)
    {
        await UpdateStateAsync(
            state =>
            {
                state.Volume = Math.Clamp(volume, 0, 100);
            },
            notify
        );
    }

    /// <summary>
    /// Начать воспроизведение трека
    /// </summary>
    public async Task StartPlayingAsync(BaseTrackInfo track, bool notify = true)
    {
        await StartPlayingAsync(track, null, null, notify);
    }

    /// <summary>
    /// Начать воспроизведение трека с информацией о пользователе, заказавшем трек
    /// </summary>
    public async Task StartPlayingAsync(
        BaseTrackInfo track,
        string? requestedBy,
        string? requestedByDisplayName,
        bool notify = true
    )
    {
        await UpdateStateAsync(
            state =>
            {
                state.CurrentTrack = track;
                state.CurrentTrackDuration = track.Duration;
                state.IsPaused = false;
                state.IsStoped = false;
                state.CurrentTrackRequestedBy = requestedBy;
                state.CurrentTrackRequestedByDisplayName = requestedByDisplayName;
            },
            notify
        );
    }

    /// <summary>
    /// Остановить воспроизведение и очистить состояние
    /// </summary>
    public async Task StopPlaybackAsync(bool notify = true)
    {
        await UpdateStateAsync(
            state =>
            {
                state.CurrentTrack = null;
                state.NextTrack = null;
                state.CurrentTrackDuration = null;
                state.IsPaused = false;
                state.IsStoped = true;
                state.CurrentTrackRequestedBy = null;
                state.CurrentTrackRequestedByDisplayName = null;
            },
            notify
        );
    }

    /// <summary>
    /// Уведомить подписчиков об изменении состояния вручную
    /// </summary>
    public async Task NotifyStateChangedAsync()
    {
        var state = await GetStateAsync();
        if (StateChanged != null)
        {
            await StateChanged.Invoke(state);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _semaphore.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
