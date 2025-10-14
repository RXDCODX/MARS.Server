using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Основной контроллер аудиоплеера с интеграцией всех сервисов
/// </summary>
public class MainPlayer(
    StateManager stateManager,
    SignalRService signalRService,
    SoundRequestUserQueue queue,
    IDbContextFactory<AppDbContext> dbFactory,
    IHostApplicationLifetime lifetime
) : IPlayerController, IDisposable
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;
    private bool _disposed;

    #region IPlayerController Events

    /// <summary>
    /// Событие начала воспроизведения трека
    /// </summary>
    public event Func<BaseTrackInfo, Task>? OnStarted;

    /// <summary>
    /// Событие завершения воспроизведения трека
    /// </summary>
    public event Func<BaseTrackInfo, Task>? OnEnded;

    /// <summary>
    /// Событие ошибки воспроизведения
    /// </summary>
    public event Func<BaseTrackInfo, Task>? OnError;

    #endregion

    #region Initialization

    /// <summary>
    /// Инициализация плеера и подписка на события состояния
    /// </summary>
    public void Initialize()
    {
        // Подписываемся на изменения состояния для отправки через SignalR
        stateManager.StateChanged += async (state) =>
        {
            await signalRService.NotifyPlayerStateChangedAsync(state);
        };
    }

    #endregion

    #region IPlayerController Implementation

    /// <summary>
    /// Начать воспроизведение трека
    /// </summary>
    public async Task PlayAsync(BaseTrackInfo track, CancellationToken ct)
    {
        await PlayAsync(track, null, null, ct);
    }

    /// <summary>
    /// Начать воспроизведение трека с информацией о пользователе, заказавшем трек
    /// </summary>
    public async Task PlayAsync(
        BaseTrackInfo track,
        string? requestedBy,
        string? requestedByDisplayName,
        CancellationToken ct
    )
    {
        try
        {
            // Обновляем состояние - начинаем воспроизведение
            await stateManager.StartPlayingAsync(
                track,
                requestedBy,
                requestedByDisplayName,
                notify: true
            );

            // Обновляем время последнего воспроизведения в БД
            await UpdateTrackLastPlayedAsync(track);

            // Загружаем следующий трек из очереди
            await LoadNextTrackAsync();

            // Вызываем событие начала воспроизведения
            if (OnStarted != null)
            {
                await OnStarted.Invoke(track);
            }
        }
        catch (Exception)
        {
            // При ошибке вызываем событие ошибки
            if (OnError != null)
            {
                await OnError.Invoke(track);
            }
        }
    }

    /// <summary>
    /// Приостановить воспроизведение
    /// </summary>
    public async Task PauseAsync(CancellationToken ct)
    {
        await stateManager.SetPausedAsync(true, notify: true);
    }

    /// <summary>
    /// Возобновить воспроизведение
    /// </summary>
    public async Task ResumeAsync(CancellationToken ct)
    {
        await stateManager.SetPausedAsync(false, notify: true);
    }

    /// <summary>
    /// Остановить воспроизведение
    /// </summary>
    public async Task StopAsync(CancellationToken ct)
    {
        await stateManager.StopPlaybackAsync(notify: true);
    }

    /// <summary>
    /// Пропустить текущий трек и воспроизвести следующий
    /// </summary>
    public async Task SkipAsync(CancellationToken ct)
    {
        var currentState = await stateManager.GetStateAsync();
        var currentTrack = currentState.CurrentTrack;

        // Вызываем событие завершения для текущего трека
        if (currentTrack != null && OnEnded != null)
        {
            await OnEnded.Invoke(currentTrack);
        }

        // Воспроизводим следующий трек из очереди
        await PlayNextFromQueueAsync();
    }

    /// <summary>
    /// Установить громкость
    /// </summary>
    public async Task SetVolumeAsync(int volume, CancellationToken ct)
    {
        await stateManager.SetVolumeAsync(volume, notify: true);
    }

    /// <summary>
    /// Отключить звук
    /// </summary>
    public async Task MuteAsync(CancellationToken ct)
    {
        await stateManager.SetMutedAsync(true, notify: true);
    }

    /// <summary>
    /// Включить звук
    /// </summary>
    public async Task UnmuteAsync(CancellationToken ct)
    {
        await stateManager.SetMutedAsync(false, notify: true);
    }

    /// <summary>
    /// Получить текущее состояние плеера
    /// </summary>
    public PlayerState GetState()
    {
        return stateManager.GetState();
    }

    #endregion

    #region Queue Management

    /// <summary>
    /// Воспроизвести следующий трек из очереди
    /// </summary>
    public async Task PlayNextFromQueueAsync()
    {
        var nextTrack = await queue.GetNextTrackAsync();

        if (nextTrack != null)
        {
            // Воспроизводим трек с информацией о пользователе
            await PlayAsync(
                nextTrack.RequestedTrack,
                nextTrack.TwitchId,
                nextTrack.TwitchDisplayName,
                _cancellationToken
            );

            // Удаляем из очереди
            await queue.RemoveFromQueueAsync(nextTrack.Id);

            // Уведомляем об изменении очереди
            await NotifyQueueChangedAsync();
        }
        else
        {
            // Очередь пуста - останавливаем воспроизведение
            await StopAsync(_cancellationToken);
        }
    }

    /// <summary>
    /// Загрузить информацию о следующем треке в состояние
    /// </summary>
    private async Task LoadNextTrackAsync()
    {
        var nextTrack = await queue.GetNextTrackAsync();
        await stateManager.SetNextTrackAsync(nextTrack?.RequestedTrack, notify: true);
    }

    /// <summary>
    /// Уведомить клиентов об изменении очереди
    /// </summary>
    private async Task NotifyQueueChangedAsync()
    {
        var currentQueue = await queue.GetQueueAsync();
        await signalRService.NotifyQueueChangedAsync(currentQueue);
    }

    #endregion

    #region Track Event Handlers

    /// <summary>
    /// Вызывается когда трек завершился на фронтенде
    /// </summary>
    public async Task OnTrackEndedAsync()
    {
        var currentState = await stateManager.GetStateAsync();
        var currentTrack = currentState.CurrentTrack;

        // Вызываем событие завершения
        if (currentTrack != null && OnEnded != null)
        {
            await OnEnded.Invoke(currentTrack);
        }

        // Если плеер не остановлен и не на паузе - воспроизводим следующий
        if (!currentState.IsStoped && !currentState.IsPaused)
        {
            await PlayNextFromQueueAsync();
        }
    }

    /// <summary>
    /// Вызывается когда трек начал воспроизведение на фронтенде
    /// </summary>
    public async Task OnTrackStartedAsync()
    {
        var currentState = await stateManager.GetStateAsync();
        var currentTrack = currentState.CurrentTrack;

        if (currentTrack != null)
        {
            await UpdateTrackLastPlayedAsync(currentTrack);
        }

        // Уведомляем клиентов об обновлении состояния
        await stateManager.NotifyStateChangedAsync();
    }

    /// <summary>
    /// Вызывается при ошибке воспроизведения на фронтенде
    /// </summary>
    public async Task OnTrackErrorAsync()
    {
        var currentState = await stateManager.GetStateAsync();
        var currentTrack = currentState.CurrentTrack;

        // Вызываем событие ошибки
        if (currentTrack != null && OnError != null)
        {
            await OnError.Invoke(currentTrack);
        }

        // Пытаемся воспроизвести следующий трек
        await PlayNextFromQueueAsync();
    }

    #endregion

    #region Database Operations

    /// <summary>
    /// Обновить время последнего воспроизведения трека в базе данных
    /// </summary>
    private async Task UpdateTrackLastPlayedAsync(BaseTrackInfo track)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(_cancellationToken);

            var dbTrack = await db.SoundRequestBaseTrackInfos.FirstOrDefaultAsync(
                t => t.Id == track.Id,
                _cancellationToken
            );

            if (dbTrack != null)
            {
                dbTrack.LastTimePlays = DateTime.UtcNow;
                db.SoundRequestBaseTrackInfos.Update(dbTrack);
                await db.SaveChangesAsync(_cancellationToken);
            }
        }
        catch (Exception)
        {
            // Игнорируем ошибки обновления БД, это не критично
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}
