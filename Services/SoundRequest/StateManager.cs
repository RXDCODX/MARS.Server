using System.Linq;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Менеджер состояния плеера с поддержкой многопоточности и персистентностью в БД
/// </summary>
public class StateManager(
    IDbContextFactory<AppDbContext> dbFactory,
    IHostApplicationLifetime lifetime,
    ILogger<StateManager> logger
) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;
    private PlayerState _currentState = new()
    {
        Id = Guid.NewGuid(),
        Volume = 100f,
        State = PlaybackState.Stopped,
        IsMuted = false,
        PausedByMute = false,
    };
    private bool _disposed;
    private bool _isInitialized;

    /// <summary>
    /// Событие изменения состояния плеера
    /// </summary>
    /// <remarks>
    /// Параметры: (PlayerState state, string? excludeConnectionId)
    /// excludeConnectionId - ID соединения, которое нужно исключить из рассылки уведомлений
    /// </remarks>
    public event Func<PlayerState, string?, Task>? StateChanged;

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

            await using var db = await dbFactory.CreateDbContextAsync(_cancellationToken);

            // Пытаемся загрузить существующее состояние из БД
            var dbState = await db
                .SoundRequestPlayerState.AsNoTracking()
                .Include(s => s.CurrentQueueItem)
                .ThenInclude(qi => qi!.Track)
                .Include(s => s.CurrentQueueItem)
                .ThenInclude(qi => qi!.RequestedByTwitchUser)
                .FirstOrDefaultAsync(_cancellationToken);

            if (dbState != null)
            {
                dbState.State = PlaybackState.Stopped;

                if (dbState.State == PlaybackState.Stopped)
                {
                    dbState.CurrentTrackProgress = TimeSpan.Zero;
                }

                logger.LogInformation(
                    "Загружено состояние плеера из БД: ID={StateId}, State={State}, Volume={Volume}, CurrentQueueItem={CurrentQueueItem}, CurrentQueueItemId={CurrentQueueItemId}",
                    dbState.Id,
                    dbState.State,
                    dbState.Volume,
                    dbState.CurrentQueueItem?.Track?.TrackName ?? "null",
                    dbState.CurrentQueueItemId?.ToString() ?? "null"
                );
                _currentState = dbState;
            }
            else
            {
                // Создаем новое состояние в БД
                logger.LogInformation("Состояние плеера не найдено в БД, создаем новое");
                db.SoundRequestPlayerState.Add(_currentState);
                await db.SaveChangesAsync(_cancellationToken);
            }

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            logger.LogError(
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
        await _semaphore.WaitAsync(_cancellationToken);
        try
        {
            // Возвращаем копию состояния
            return new PlayerState
            {
                Id = _currentState.Id,
                StateVersion = _currentState.StateVersion,
                CurrentQueueItemId = _currentState.CurrentQueueItemId,
                CurrentQueueItem = _currentState.CurrentQueueItem,
                CurrentTrackProgress = _currentState.CurrentTrackProgress,
                State = _currentState.State,
                VideoState = _currentState.VideoState,
                IsMuted = _currentState.IsMuted,
                Volume = _currentState.Volume,
                PausedByMute = _currentState.PausedByMute,
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
        _semaphore.Wait(_cancellationToken);
        try
        {
            return new PlayerState
            {
                Id = _currentState.Id,
                StateVersion = _currentState.StateVersion,
                CurrentQueueItemId = _currentState.CurrentQueueItemId,
                CurrentQueueItem = _currentState.CurrentQueueItem,
                CurrentTrackProgress = _currentState.CurrentTrackProgress,
                State = _currentState.State,
                VideoState = _currentState.VideoState,
                IsMuted = _currentState.IsMuted,
                Volume = _currentState.Volume,
                PausedByMute = _currentState.PausedByMute,
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
            await using var db = await dbFactory.CreateDbContextAsync(_cancellationToken);

            // Проверяем существует ли запись в БД
            var existingState = await db.SoundRequestPlayerState.FindAsync(
                [_currentState.Id],
                cancellationToken: _cancellationToken
            );

            if (existingState != null)
            {
                // Обновляем существующую запись
                existingState.CurrentQueueItemId = _currentState.CurrentQueueItemId;
                existingState.CurrentTrackProgress = _currentState.CurrentTrackProgress;
                existingState.State = _currentState.State;
                existingState.VideoState = _currentState.VideoState;
                existingState.IsMuted = _currentState.IsMuted;
                existingState.PausedByMute = _currentState.PausedByMute;
                existingState.Volume = _currentState.Volume;

                db.SoundRequestPlayerState.Update(existingState);
            }
            else
            {
                // Создаем новую запись (на случай если её удалили)
                db.SoundRequestPlayerState.Add(_currentState);
            }

            await db.SaveChangesAsync(_cancellationToken);

            logger.LogDebug(
                "Состояние плеера сохранено в БД: State={State}, Volume={Volume}",
                _currentState.State,
                _currentState.Volume
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при сохранении состояния плеера в БД");
        }
    }

    /// <summary>
    /// Обновить состояние плеера (потокобезопасно)
    /// </summary>
    /// <param name="updateAction">Действие для обновления состояния</param>
    /// <param name="notifyStateChanged">Уведомлять ли подписчиков об изменении</param>
    /// <param name="excludeConnectionId">ID соединения, которое нужно исключить из рассылки уведомлений</param>
    public async Task UpdateStateAsync(
        Action<PlayerState> updateAction,
        bool notifyStateChanged = true,
        string? excludeConnectionId = null
    )
    {
        PlayerState? stateToNotify = null;

        await _semaphore.WaitAsync(_cancellationToken);
        try
        {
            updateAction(_currentState);

            // Генерируем новую версию состояния при каждом изменении
            _currentState.StateVersion = Guid.NewGuid();

            if (notifyStateChanged)
            {
                stateToNotify = new PlayerState
                {
                    Id = _currentState.Id,
                    StateVersion = _currentState.StateVersion,
                    CurrentQueueItemId = _currentState.CurrentQueueItemId,
                    CurrentQueueItem = _currentState.CurrentQueueItem,
                    CurrentTrackProgress = _currentState.CurrentTrackProgress,
                    State = _currentState.State,
                    VideoState = _currentState.VideoState,
                    IsMuted = _currentState.IsMuted,
                    Volume = _currentState.Volume,
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
            await StateChanged.Invoke(stateToNotify, excludeConnectionId);
        }
    }

    /// <summary>
    /// Установить текущий элемент очереди
    /// </summary>
    public async Task SetCurrentQueueItemAsync(
        QueueItem? queueItem,
        bool notify = true,
        string? excludeConnectionId = null
    )
    {
        await UpdateStateAsync(
            state =>
            {
                state.CurrentQueueItemId = queueItem?.Id;
                state.CurrentQueueItem = queueItem;
                state.State =
                    queueItem == null ? PlaybackState.Stopped : PlaybackState.WaitingForTrack;
            },
            notify,
            excludeConnectionId
        );
    }

    /// Установить состояние воспроизведения
    /// </summary>
    public async Task SetPlaybackStateAsync(
        PlaybackState playbackState,
        bool notify = true,
        string? excludeConnectionId = null
    )
    {
        await UpdateStateAsync(
            state =>
            {
                state.State = playbackState;
                if (playbackState == PlaybackState.Stopped)
                {
                    state.CurrentQueueItemId = null;
                    state.CurrentQueueItem = null;
                    state.CurrentTrackProgress = null;
                }
            },
            notify,
            excludeConnectionId
        );
    }

    /// <summary>
    /// Установить состояние паузы
    /// </summary>
    public async Task SetPausedAsync(
        bool isPaused,
        bool notify = true,
        string? excludeConnectionId = null
    )
    {
        await SetPlaybackStateAsync(
            isPaused ? PlaybackState.Paused : PlaybackState.Playing,
            notify,
            excludeConnectionId
        );
    }

    /// <summary>
    /// Установить состояние отключения звука
    /// </summary>
    public async Task SetMutedAsync(
        bool isMuted,
        bool notify = true,
        string? excludeConnectionId = null
    )
    {
        await UpdateStateAsync(state => state.IsMuted = isMuted, notify, excludeConnectionId);
    }

    /// <summary>
    /// Установить флаг, что пауза была вызвана глобальным mute (PausedByMute)
    /// </summary>
    public async Task SetPausedByMuteAsync(
        bool byMute,
        bool notify = true,
        string? excludeConnectionId = null
    )
    {
        await UpdateStateAsync(state => state.PausedByMute = byMute, notify, excludeConnectionId);
    }

    /// <summary>
    /// Установить громкость (0.0 - 100.0)
    /// </summary>
    public async Task SetVolumeAsync(
        float volume,
        bool notify = true,
        string? excludeConnectionId = null
    )
    {
        await UpdateStateAsync(
            state =>
            {
                state.Volume = Math.Clamp(volume, 0f, 100f);
            },
            notify,
            excludeConnectionId
        );
    }

    /// <summary>
    /// Установить режим отображения видео
    /// </summary>
    public async Task SetVideoDisplayAsync(
        VideoDisplay videoDisplay,
        bool notify = true,
        string? excludeConnectionId = null
    )
    {
        await UpdateStateAsync(
            state => state.VideoState = videoDisplay,
            notify,
            excludeConnectionId
        );
    }

    /// <summary>
    /// Обновить текущий прогресс воспроизведения трека
    /// </summary>
    public async Task UpdateCurrentTrackProgressAsync(
        TimeSpan progress,
        bool notify = false,
        string? excludeConnectionId = null
    )
    {
        await UpdateStateAsync(
            state =>
            {
                if (state.CurrentQueueItem != null)
                {
                    state.CurrentTrackProgress = progress;
                }
            },
            notify,
            excludeConnectionId
        );
    }

    /// <summary>
    /// Начать воспроизведение элемента очереди
    /// </summary>
    public async Task StartPlayingAsync(
        QueueItem queueItem,
        bool notify = true,
        string? excludeConnectionId = null
    )
    {
        await UpdateStateAsync(
            state =>
            {
                state.CurrentQueueItemId = queueItem.Id;
                state.CurrentQueueItem = queueItem;
                state.State = PlaybackState.Playing;
                state.CurrentTrackProgress = TimeSpan.Zero;
            },
            notify,
            excludeConnectionId
        );
    }

    /// <summary>
    /// Остановить воспроизведение и очистить состояние
    /// </summary>
    public async Task StopPlaybackAsync(bool notify = true, string? excludeConnectionId = null)
    {
        await UpdateStateAsync(
            state =>
            {
                state.CurrentQueueItemId = null;
                state.CurrentQueueItem = null;
                state.CurrentTrackProgress = null;
                state.State = PlaybackState.Stopped;
            },
            notify,
            excludeConnectionId
        );
    }

    /// <summary>
    /// Уведомить подписчиков об изменении состояния вручную
    /// </summary>
    /// <param name="excludeConnectionId">ID соединения, которое нужно исключить из рассылки</param>
    public async Task NotifyStateChangedAsync(string? excludeConnectionId = null)
    {
        var state = await GetStateAsync();
        if (StateChanged != null)
        {
            await StateChanged.Invoke(state, excludeConnectionId);
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

    private async Task<QueueItem?> GetNextQueueItemAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync(_cancellationToken);

        return await db
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .FirstOrDefaultAsync(qi => qi.QueueOrder == 1, _cancellationToken);
    }

    private QueueItem? GetNextQueueItem()
    {
        using var db = dbFactory.CreateDbContext();

        return db
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .FirstOrDefault(qi => qi.QueueOrder == 1);
    }
}
