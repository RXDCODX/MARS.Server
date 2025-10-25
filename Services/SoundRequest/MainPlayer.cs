using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Основной контроллер аудиоплеера с интеграцией всех сервисов
/// </summary>
public class MainPlayer : IPlayerController, IHostedService, IDisposable
{
    private readonly CancellationToken _cancellationToken;
    private bool _disposed;
    private readonly StateManager _stateManager;
    private readonly InSignalRHubService _inSignalRHubService;
    private readonly SoundRequestUserQueue _queue;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<MainPlayer> _logger;

    /// <summary>
    /// Основной контроллер аудиоплеера с интеграцией всех сервисов
    /// </summary>
    public MainPlayer(
        StateManager stateManager,
        InSignalRHubService inSignalRHubService,
        OutSignalRHubService outSignalRHubService,
        SoundRequestUserQueue queue,
        IDbContextFactory<AppDbContext> dbFactory,
        IHostApplicationLifetime lifetime,
        ILogger<MainPlayer> logger
    )
    {
        _stateManager = stateManager;
        _inSignalRHubService = inSignalRHubService;
        _queue = queue;
        _dbFactory = dbFactory;
        _logger = logger;
        _cancellationToken = lifetime.ApplicationStopping;

        outSignalRHubService.OnEnded += OutSignalRHubServiceOnEnded;
        outSignalRHubService.OnStarted += OutSignalRHubServiceOnStarted;
        outSignalRHubService.OnError += OutSignalRHubServiceOnError;
    }

    #region IHostedService Implementation

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync();
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion

    #region SignalREventsHandlers

    /// <summary>
    /// Обработчик события ошибки воспроизведения с фронтенда
    /// </summary>
    private async Task OutSignalRHubServiceOnError(BaseTrackInfo arg)
    {
        _logger.LogError(
            "[SignalR Event] Получена ошибка воспроизведения трека: {TrackName} (ID: {TrackId})",
            arg.TrackName,
            arg.Id
        );

        await OnTrackErrorAsync(arg);
    }

    /// <summary>
    /// Обработчик события начала воспроизведения трека с фронтенда
    /// </summary>
    private async Task OutSignalRHubServiceOnStarted(BaseTrackInfo arg)
    {
        _logger.LogInformation(
            "[SignalR Event] Трек начал воспроизведение: {TrackName} (ID: {TrackId})",
            arg.TrackName,
            arg.Id
        );

        await OnTrackStartedAsync(arg);
    }

    /// <summary>
    /// Обработчик события завершения воспроизведения трека с фронтенда
    /// </summary>
    private async Task OutSignalRHubServiceOnEnded(BaseTrackInfo arg)
    {
        _logger.LogInformation(
            "[SignalR Event] Трек завершил воспроизведение: {TrackName} (ID: {TrackId})",
            arg.TrackName,
            arg.Id
        );

        await OnTrackEndedAsync(arg);
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Инициализация плеера и подписка на события состояния
    /// </summary>
    public async Task InitializeAsync()
    {
        // Инициализируем StateManager (загружаем состояние из БД)
        await _stateManager.InitializeAsync();

        // Подписываемся на изменения состояния для отправки через SignalR
        _stateManager.StateChanged += async (state) =>
        {
            await _inSignalRHubService.NotifyPlayerStateChangedAsync(state);
        };

        // Загружаем следующий элемент очереди, если он еще не загружен
        var currentState = await _stateManager.GetStateAsync();
        var queueCount = (await _queue.GetQueueAsync()).Count;

        _logger.LogInformation(
            "[InitializeAsync] Текущее состояние: State={State}, CurrentQueueItem={CurrentQueueItem}, NextQueueItem={NextQueueItem}, QueueCount={QueueCount}",
            currentState.State,
            currentState.CurrentQueueItem?.Track?.TrackName ?? "null",
            currentState.NextQueueItem?.Track?.TrackName ?? "null",
            queueCount
        );

        if (currentState.NextQueueItem == null && queueCount > 0)
        {
            _logger.LogInformation(
                "[InitializeAsync] Следующий элемент очереди не установлен, но в очереди есть {QueueCount} элементов, загружаем...",
                queueCount
            );
            await LoadNextQueueItemAsync();

            var updatedState = await _stateManager.GetStateAsync();
            _logger.LogInformation(
                "[InitializeAsync] После загрузки: NextQueueItem={NextQueueItem}",
                updatedState.NextQueueItem?.Track?.TrackName ?? "null"
            );
        }
        else if (queueCount == 0)
        {
            _logger.LogInformation(
                "[InitializeAsync] Очередь пуста, следующий элемент не загружается"
            );
        }
    }

    #endregion

    #region IPlayerController Implementation

    /// <summary>
    /// Начать воспроизведение элемента очереди
    /// </summary>
    public async Task PlayAsync(QueueItem queueItem, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Начинаем воспроизведение трека: {TrackName}, URL: {Url}",
                queueItem.Track!.TrackName,
                queueItem.Track.Url
            );

            // Обновляем состояние - начинаем воспроизведение
            await _stateManager.StartPlayingAsync(queueItem, notify: true);

            _logger.LogDebug("Состояние обновлено, уведомление отправлено");

            // Обновляем время последнего воспроизведения в БД
            await UpdateQueueItemLastPlayedAsync(queueItem);

            // Загружаем следующий элемент из очереди
            await LoadNextQueueItemAsync();

            _logger.LogInformation("Трек успешно запущен: {TrackName}", queueItem.Track.TrackName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при воспроизведении трека: {TrackName}",
                queueItem.Track!.TrackName
            );
            // При ошибке вызываем событие ошибки
            await OutSignalRHubServiceOnError(queueItem.Track);
        }
    }

    /// <summary>
    /// Начать воспроизведение трека (устаревший метод для обратной совместимости)
    /// </summary>
    [Obsolete("Используйте PlayAsync(QueueItem)")]
    public async Task PlayAsync(BaseTrackInfo track, TwitchUser? user, CancellationToken ct)
    {
        // Создаем временный QueueItem для обратной совместимости
        var queueItem = new QueueItem
        {
            TrackId = track.Id,
            Track = track,
            RequestedByTwitchId = user?.TwitchId ?? string.Empty,
            RequestedByTwitchUser =
                user
                ?? new TwitchUser
                {
                    TwitchId = string.Empty,
                    UserLogin = string.Empty,
                    DisplayName = string.Empty,
                },
        };

        await PlayAsync(queueItem, ct);
    }

    /// <summary>
    /// Приостановить воспроизведение
    /// </summary>
    public async Task PauseAsync(CancellationToken ct)
    {
        await _stateManager.SetPausedAsync(true, notify: true);
    }

    /// <summary>
    /// Возобновить воспроизведение
    /// </summary>
    public async Task ResumeAsync(CancellationToken ct)
    {
        await _stateManager.SetPausedAsync(false, notify: true);
    }

    /// <summary>
    /// Остановить воспроизведение
    /// </summary>
    public async Task StopAsync(CancellationToken ct)
    {
        await _stateManager.StopPlaybackAsync(notify: true);
    }

    /// <summary>
    /// Пропустить текущий трек и воспроизвести следующий
    /// </summary>
    public async Task SkipAsync(CancellationToken ct)
    {
        _logger.LogInformation("Пропуск текущего трека");

        // Воспроизводим следующий трек из очереди
        await PlayNextFromQueueAsync();
    }

    /// <summary>
    /// Установить громкость (0.0 - 100.0)
    /// </summary>
    public async Task SetVolumeAsync(float volume, CancellationToken ct)
    {
        await _stateManager.SetVolumeAsync(volume, notify: true);
    }

    /// <summary>
    /// Отключить звук
    /// </summary>
    public async Task MuteAsync(CancellationToken ct)
    {
        await _stateManager.SetMutedAsync(true, notify: true);
    }

    /// <summary>
    /// Включить звук
    /// </summary>
    public async Task UnmuteAsync(CancellationToken ct)
    {
        await _stateManager.SetMutedAsync(false, notify: true);
    }

    /// <summary>
    /// Установить режим отображения видео
    /// </summary>
    public async Task SetVideoDisplayAsync(VideoDisplay videoDisplay, CancellationToken ct)
    {
        await _stateManager.SetVideoDisplayAsync(videoDisplay, notify: true);
    }

    /// <summary>
    /// Получить текущее состояние плеера
    /// </summary>
    public PlayerState GetState()
    {
        return _stateManager.GetState();
    }

    #endregion

    #region Queue Management

    /// <summary>
    /// Воспроизвести следующий элемент из очереди
    /// </summary>
    public async Task PlayNextFromQueueAsync()
    {
        var nextQueueItem = await _queue.GetNextQueueItemAsync();

        _logger.LogDebug(
            "Следующий элемент очереди: {TrackName}",
            nextQueueItem != null ? nextQueueItem.Track!.TrackName : "null"
        );

        if (nextQueueItem != null)
        {
            _logger.LogInformation(
                "Начинаем воспроизведение следующего трека: {TrackName}",
                nextQueueItem.Track!.TrackName
            );

            // Воспроизводим трек
            await PlayAsync(nextQueueItem, _cancellationToken);

            // Удаляем из очереди
            await _queue.RemoveFromQueueAsync(nextQueueItem.Id);

            // Уведомляем об изменении очереди
            await NotifyQueueChangedAsync();

            _logger.LogInformation(
                "Трек из очереди успешно запущен: {TrackName}",
                nextQueueItem.Track.TrackName
            );
        }
        else
        {
            _logger.LogInformation("Очередь пуста - останавливаем плеер");
            // Очередь пуста - останавливаем воспроизведение
            await StopAsync(_cancellationToken);
        }
    }

    /// <summary>
    /// Загрузить информацию о следующем элементе очереди в состояние
    /// </summary>
    private async Task LoadNextQueueItemAsync()
    {
        var nextQueueItem = await _queue.GetNextQueueItemAsync();
        await _stateManager.SetNextQueueItemAsync(nextQueueItem, notify: true);
    }

    /// <summary>
    /// Уведомить клиентов об изменении очереди
    /// </summary>
    private async Task NotifyQueueChangedAsync()
    {
        var currentQueue = await _queue.GetQueueAsync();
        await _inSignalRHubService.NotifyQueueChangedAsync(currentQueue);
    }

    #endregion

    #region Track Event Handlers

    /// <summary>
    /// Вызывается когда трек завершился на фронтенде
    /// </summary>
    /// <param name="track">Информация о завершенном треке</param>
    public async Task OnTrackEndedAsync(BaseTrackInfo track)
    {
        var currentState = await _stateManager.GetStateAsync();

        _logger.LogInformation(
            "Обработка завершения трека: {TrackName} (ID: {TrackId})",
            track.TrackName,
            track.Id
        );

        // Если плеер не остановлен и не на паузе - воспроизводим следующий
        if (currentState.State == PlaybackState.Playing)
        {
            await PlayNextFromQueueAsync();
        }
    }

    /// <summary>
    /// Вызывается когда трек начал воспроизведение на фронтенде
    /// </summary>
    /// <param name="track">Информация о начавшем воспроизведение треке</param>
    public async Task OnTrackStartedAsync(BaseTrackInfo track)
    {
        _logger.LogInformation(
            "Обработка начала воспроизведения трека: {TrackName} (ID: {TrackId})",
            track.TrackName,
            track.Id
        );

        // Обновление времени последнего воспроизведения теперь происходит в PlayAsync → UpdateQueueItemLastPlayedAsync

        // Уведомляем клиентов об обновлении состояния
        await _stateManager.NotifyStateChangedAsync();
    }

    /// <summary>
    /// Вызывается при ошибке воспроизведения на фронтенде
    /// </summary>
    /// <param name="track">Информация о треке, при воспроизведении которого произошла ошибка</param>
    public async Task OnTrackErrorAsync(BaseTrackInfo track)
    {
        _logger.LogError(
            "Обработка ошибки воспроизведения трека: {TrackName} (ID: {TrackId})",
            track.TrackName,
            track.Id
        );

        // Пытаемся воспроизвести следующий трек
        await PlayNextFromQueueAsync();
    }

    #endregion

    #region Database Operations

    /// <summary>
    /// Обновить время последнего воспроизведения элемента очереди в базе данных
    /// </summary>
    private async Task UpdateQueueItemLastPlayedAsync(QueueItem queueItem)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);

            var dbQueueItem = await db.SoundRequestQueueItems.FirstOrDefaultAsync(
                qi => qi.Id == queueItem.Id,
                _cancellationToken
            );

            if (dbQueueItem != null)
            {
                // Обновляем время последнего воспроизведения в треке
                var dbTrack = await db.SoundRequestBaseTrackInfos.FirstOrDefaultAsync(
                    t => t.Id == queueItem.TrackId,
                    _cancellationToken
                );

                if (dbTrack != null)
                {
                    dbTrack.LastTimePlays = DateTime.UtcNow;
                    db.SoundRequestBaseTrackInfos.Update(dbTrack);
                    await db.SaveChangesAsync(_cancellationToken);
                }
            }
        }
        catch (Exception)
        {
            // Игнорируем ошибки обновления БД, это не критично
        }
    }

    #endregion

    #region Extended Player Control Methods (из SoundRequestManager)

    /// <summary>
    /// Воспроизвести плеер (Resume или начать воспроизведение следующего трека)
    /// </summary>
    public async Task PlayAsync()
    {
        PlayerState? state = null;
        var queueCount = 0;

        state = GetState();
        queueCount = (await _queue.GetQueueAsync()).Count;

        _logger.LogDebug(
            "[PlayAsync] State: State={State}, HasCurrentQueueItem={HasCurrentQueueItem}, QueueCount={QueueCount}",
            state.State,
            state.CurrentQueueItem != null,
            queueCount
        );

        // Если плеер остановлен или нет текущего элемента очереди, начинаем воспроизведение следующего
        if (state.State == PlaybackState.Stopped || state.CurrentQueueItem == null)
        {
            _logger.LogDebug("[PlayAsync] Вызываем PlayNextFromQueueAsync");
            await PlayNextFromQueueAsync();
        }
        else
        {
            // Иначе просто снимаем паузу
            _logger.LogDebug("[PlayAsync] Вызываем ResumeAsync");
            await ResumeAsync(_cancellationToken);
        }
    }

    /// <summary>
    /// Переключить воспроизведение (Play/Pause)
    /// </summary>
    public async Task TogglePlayPauseAsync()
    {
        var state = GetState();

        if (state.State == PlaybackState.Paused)
        {
            await PlayAsync();
        }
        else
        {
            await PauseAsync(_cancellationToken);
        }
    }

    #endregion

    #region Extended Queue Management Methods (из SoundRequestManager)

    /// <summary>
    /// Получить очередь элементов
    /// </summary>
    public async Task<List<QueueItem>> GetQueueAsync()
    {
        return await _queue.GetQueueAsync();
    }

    /// <summary>
    /// Воспроизвести конкретный элемент из очереди
    /// </summary>
    public async Task PlayQueueItemAsync(Guid queueItemId)
    {
        var queueItem = await _queue.GetQueueItemByIdAsync(queueItemId);

        if (queueItem != null)
        {
            // Воспроизводим выбранный элемент
            await PlayAsync(queueItem, _cancellationToken);

            // Удаляем из очереди
            await _queue.RemoveFromQueueAsync(queueItemId);

            // Уведомляем об изменении очереди
            await NotifyQueueChangedAsync();
        }
    }

    /// <summary>
    /// Удалить элемент из очереди
    /// </summary>
    public async Task RemoveQueueItemAsync(Guid queueItemId)
    {
        await _queue.RemoveFromQueueAsync(queueItemId);
    }

    #endregion

    #region History Management (из SoundRequestManager)

    /// <summary>
    /// Получить историю воспроизведенных треков
    /// </summary>
    public async Task<List<BaseTrackInfo>> GetHistoryAsync(int count = 20)
    {
        List<BaseTrackInfo> result = [];

        await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);

        result = await db
            .SoundRequestBaseTrackInfos.AsNoTracking()
            .Where(t => t.LastTimePlays != DateTime.UnixEpoch)
            .OrderByDescending(t => t.LastTimePlays)
            .Take(count)
            .ToListAsync(_cancellationToken);

        return result;
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
