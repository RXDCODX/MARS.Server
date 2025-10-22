using MARS.Server.DataBaseContext;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Менеджер состояния плеера с поддержкой многопоточности и персистентностью в БД
/// </summary>
public class StateManager : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<StateManager> _logger;
    private readonly CancellationToken _cancellationToken;
    private PlayerState _currentState;
    private bool _disposed;
    private bool _isInitialized;

    /// <summary>
    /// Событие изменения состояния плеера
    /// </summary>
    public event Func<PlayerState, Task>? StateChanged;

    public StateManager(
        IDbContextFactory<AppDbContext> dbFactory,
        IHostApplicationLifetime lifetime,
        ILogger<StateManager> logger
    )
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _cancellationToken = lifetime.ApplicationStopping;
        _currentState = new PlayerState
        {
            Id = Guid.NewGuid(),
            Volume = 100f,
            State = PlaybackState.Stopped,
            IsMuted = false,
        };
    }

    /// <summary>
    /// Инициализация состояния из БД (вызывается один раз при старте)
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _semaphore.WaitAsync(_cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);

            // Пытаемся загрузить существующее состояние из БД
            var dbState = await db
                .SoundRequestPlayerState.AsNoTracking()
                .Include(s => s.CurrentTrack)
                .Include(s => s.NextTrack)
                .Include(s => s.CurrentTrackRequestedByTwitchUser)
                .FirstOrDefaultAsync(_cancellationToken);

            if (dbState != null)
            {
                _logger.LogInformation(
                    "Загружено состояние плеера из БД: ID={StateId}, State={State}, Volume={Volume}",
                    dbState.Id,
                    dbState.State,
                    dbState.Volume
                );
                _currentState = dbState;
            }
            else
            {
                // Создаем новое состояние в БД
                _logger.LogInformation("Состояние плеера не найдено в БД, создаем новое");
                db.SoundRequestPlayerState.Add(_currentState);
                await db.SaveChangesAsync(_cancellationToken);
            }

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при инициализации состояния плеера из БД, используем состояние по умолчанию"
            );
            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

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
                CurrentTrackId = _currentState.CurrentTrackId,
                NextTrackId = _currentState.NextTrackId,
                CurrentTrack = _currentState.CurrentTrack,
                NextTrack = _currentState.NextTrack,
                CurrentTrackDuration = _currentState.CurrentTrackDuration,
                State = _currentState.State,
                IsMuted = _currentState.IsMuted,
                Volume = _currentState.Volume,
                CurrentTrackRequestedBy = _currentState.CurrentTrackRequestedBy,
                CurrentTrackRequestedByTwitchUser = _currentState.CurrentTrackRequestedByTwitchUser,
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
                CurrentTrackId = _currentState.CurrentTrackId,
                NextTrackId = _currentState.NextTrackId,
                CurrentTrack = _currentState.CurrentTrack,
                NextTrack = _currentState.NextTrack,
                CurrentTrackDuration = _currentState.CurrentTrackDuration,
                State = _currentState.State,
                IsMuted = _currentState.IsMuted,
                Volume = _currentState.Volume,
                CurrentTrackRequestedBy = _currentState.CurrentTrackRequestedBy,
                CurrentTrackRequestedByTwitchUser = _currentState.CurrentTrackRequestedByTwitchUser,
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Сохранить текущее состояние в БД
    /// </summary>
    private async Task SaveStateToDbAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);

            // Проверяем существует ли запись в БД
            var existingState = await db.SoundRequestPlayerState.FindAsync(
                [_currentState.Id],
                cancellationToken: _cancellationToken
            );

            if (existingState != null)
            {
                // Обновляем существующую запись
                existingState.CurrentTrackId = _currentState.CurrentTrackId;
                existingState.NextTrackId = _currentState.NextTrackId;
                existingState.CurrentTrackDuration = _currentState.CurrentTrackDuration;
                existingState.State = _currentState.State;
                existingState.IsMuted = _currentState.IsMuted;
                existingState.Volume = _currentState.Volume;
                existingState.CurrentTrackRequestedBy = _currentState.CurrentTrackRequestedBy;

                db.SoundRequestPlayerState.Update(existingState);
            }
            else
            {
                // Создаем новую запись (на случай если её удалили)
                db.SoundRequestPlayerState.Add(_currentState);
            }

            await db.SaveChangesAsync(_cancellationToken);

            _logger.LogDebug(
                "Состояние плеера сохранено в БД: State={State}, Volume={Volume}",
                _currentState.State,
                _currentState.Volume
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении состояния плеера в БД");
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
                    CurrentTrackId = _currentState.CurrentTrackId,
                    NextTrackId = _currentState.NextTrackId,
                    CurrentTrack = _currentState.CurrentTrack,
                    NextTrack = _currentState.NextTrack,
                    CurrentTrackDuration = _currentState.CurrentTrackDuration,
                    State = _currentState.State,
                    IsMuted = _currentState.IsMuted,
                    Volume = _currentState.Volume,
                    CurrentTrackRequestedBy = _currentState.CurrentTrackRequestedBy,
                    CurrentTrackRequestedByTwitchUser =
                        _currentState.CurrentTrackRequestedByTwitchUser,
                };
            }
        }
        finally
        {
            _semaphore.Release();
        }

        // Сохраняем состояние в БД
        await SaveStateToDbAsync();

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
                state.CurrentTrackId = track?.Id;
                state.CurrentTrack = track;
                state.CurrentTrackDuration = track?.Duration;
                state.State = track == null ? PlaybackState.Stopped : PlaybackState.WaitingForTrack;
            },
            notify
        );
    }

    /// <summary>
    /// Установить текущий трек с информацией о пользователе, заказавшем трек
    /// </summary>
    public async Task SetCurrentTrackAsync(
        BaseTrackInfo? track,
        TwitchUser? user,
        bool notify = true
    )
    {
        await UpdateStateAsync(
            state =>
            {
                state.CurrentTrackId = track?.Id;
                state.CurrentTrack = track;
                state.CurrentTrackDuration = track?.Duration;
                state.State = track == null ? PlaybackState.Stopped : PlaybackState.WaitingForTrack;
                state.CurrentTrackRequestedBy = user?.TwitchId;
                state.CurrentTrackRequestedByTwitchUser = user;
            },
            notify
        );
    }

    /// <summary>
    /// Установить следующий трек
    /// </summary>
    public async Task SetNextTrackAsync(BaseTrackInfo? track, bool notify = true)
    {
        await UpdateStateAsync(
            state =>
            {
                state.NextTrackId = track?.Id;
                state.NextTrack = track;
            },
            notify
        );
    }

    /// <summary>
    /// Установить состояние воспроизведения
    /// </summary>
    public async Task SetPlaybackStateAsync(PlaybackState playbackState, bool notify = true)
    {
        await UpdateStateAsync(
            state =>
            {
                state.State = playbackState;
                if (playbackState == PlaybackState.Stopped)
                {
                    state.CurrentTrack = null;
                    state.CurrentTrackDuration = null;
                }
            },
            notify
        );
    }

    /// <summary>
    /// Установить состояние паузы
    /// </summary>
    public async Task SetPausedAsync(bool isPaused, bool notify = true)
    {
        await SetPlaybackStateAsync(
            isPaused ? PlaybackState.Paused : PlaybackState.Playing,
            notify
        );
    }

    /// <summary>
    /// Установить состояние остановки
    /// </summary>
    public async Task SetStoppedAsync(bool isStopped, bool notify = true)
    {
        if (isStopped)
        {
            await SetPlaybackStateAsync(PlaybackState.Stopped, notify);
        }
        else
        {
            await SetPlaybackStateAsync(PlaybackState.Playing, notify);
        }
    }

    /// <summary>
    /// Установить состояние отключения звука
    /// </summary>
    public async Task SetMutedAsync(bool isMuted, bool notify = true)
    {
        await UpdateStateAsync(state => state.IsMuted = isMuted, notify);
    }

    /// <summary>
    /// Установить громкость (0.0 - 100.0)
    /// </summary>
    public async Task SetVolumeAsync(float volume, bool notify = true)
    {
        await UpdateStateAsync(
            state =>
            {
                state.Volume = Math.Clamp(volume, 0f, 100f);
            },
            notify
        );
    }

    /// <summary>
    /// Начать воспроизведение трека с информацией о пользователе, заказавшем трек
    /// </summary>
    public async Task StartPlayingAsync(BaseTrackInfo track, TwitchUser? user, bool notify = true)
    {
        await UpdateStateAsync(
            state =>
            {
                state.CurrentTrackId = track.Id;
                state.CurrentTrack = track;
                state.CurrentTrackDuration = track.Duration;
                state.State = PlaybackState.Playing;
                state.CurrentTrackRequestedBy = user?.TwitchId;
                state.CurrentTrackRequestedByTwitchUser = user;
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
                state.CurrentTrackId = null;
                state.NextTrackId = null;
                state.CurrentTrack = null;
                state.NextTrack = null;
                state.CurrentTrackDuration = null;
                state.State = PlaybackState.Stopped;
                state.CurrentTrackRequestedBy = null;
                state.CurrentTrackRequestedByTwitchUser = null;
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
