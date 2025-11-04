using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;

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

    private readonly ITwitchClient _twitchClient;

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
        ITwitchClient twitchClient,
        ILogger<MainPlayer> logger
    )
    {
        _stateManager = stateManager;
        _inSignalRHubService = inSignalRHubService;
        _queue = queue;
        _dbFactory = dbFactory;
        _twitchClient = twitchClient;
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
        _stateManager.StateChanged += async (state, excludeConnectionId) =>
        {
            await _inSignalRHubService.NotifyPlayerStateChangedAsync(state, excludeConnectionId);
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
    public async Task PlayAsync(QueueItem queueItem, CancellationToken _)
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
        var currentState = await _stateManager.GetStateAsync();
        var currentTrack = currentState.CurrentQueueItem?.Track;

        if (currentTrack != null)
        {
            _logger.LogInformation("Пропуск текущего трека: {TrackName}", currentTrack.TrackName);

            // Отправляем сообщение в чат Твича о пропуске
            var skipMessage = $"⏩ Трек \"{currentTrack.TrackName}\" был пропущен";
            await _twitchClient.SendMessageToMainTwitchAsync(skipMessage, _logger);

            _logger.LogInformation("Отправлено сообщение в чат Твича о пропуске трека");
        }
        else
        {
            _logger.LogInformation("Пропуск текущего трека (текущий трек не найден)");
        }

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
    /// Сдвигает всю очередь на -1 и берет элемент с QueueOrder = 0 для воспроизведения
    /// </summary>
    public async Task PlayNextFromQueueAsync()
    {
        // Сдвигаем очередь и получаем элемент для воспроизведения
        var currentQueueItem = await _queue.ShiftQueueAndGetCurrentAsync();

        _logger.LogDebug(
            "Элемент для воспроизведения после сдвига очереди: {TrackName}",
            currentQueueItem != null ? currentQueueItem.Track!.TrackName : "null"
        );

        if (currentQueueItem != null)
        {
            _logger.LogInformation(
                "Начинаем воспроизведение трека: {TrackName}",
                currentQueueItem.Track!.TrackName
            );

            // Очищаем старую историю
            await CleanupOldHistoryAsync();

            // Воспроизводим трек
            await PlayAsync(currentQueueItem, _cancellationToken);

            // Уведомляем об изменении очереди
            await NotifyQueueChangedAsync();

            _logger.LogInformation(
                "Трек успешно запущен: {TrackName}",
                currentQueueItem.Track.TrackName
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
    /// Воспроизвести предыдущий трек из истории
    /// </summary>
    public async Task PlayPreviousFromHistoryAsync()
    {
        var currentState = GetState();
        await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);

        _logger.LogInformation("Запрошено воспроизведение предыдущего трека.");

        // Определяем, какой QueueOrder искать для предыдущего трека
        var targetQueueOrder = -1;

        _logger.LogInformation("Ищем трек с QueueOrder = {TargetQueueOrder}", targetQueueOrder);

        // Ищем трек с нужным QueueOrder
        var previousQueueItem = await db
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .Where(qi => qi.QueueOrder == targetQueueOrder)
            .FirstOrDefaultAsync(_cancellationToken);

        if (previousQueueItem != null)
        {
            _logger.LogInformation(
                "Воспроизводим предыдущий трек: {TrackName} (QueueOrder: {QueueOrder}), заказал: {User}",
                previousQueueItem.Track?.TrackName,
                previousQueueItem.QueueOrder,
                previousQueueItem.RequestedByTwitchUser?.DisplayName
            );

            previousQueueItem.QueueOrder = 0;
            await db.SoundRequestQueueItems.ExecuteUpdateAsync(
                e => e.SetProperty(t => t.QueueOrder, t => t.QueueOrder + 1),
                cancellationToken: _cancellationToken
            );

            await PlayAsync(previousQueueItem, _cancellationToken);

            // Загружаем следующий элемент из очереди
            await LoadNextQueueItemAsync();
        }
        else
        {
            _logger.LogWarning(
                "Не найден трек с QueueOrder = {TargetQueueOrder}. Достигнут конец истории.",
                targetQueueOrder
            );
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
    /// Проверить и загрузить текущий трек из очереди, если CurrentQueueItem пуст
    /// </summary>
    public async Task EnsureCurrentQueueItemLoadedAsync()
    {
        var currentState = await _stateManager.GetStateAsync();

        // Проверяем, пуст ли CurrentQueueItem
        if (currentState.CurrentQueueItem == null)
        {
            _logger.LogInformation(
                "[EnsureCurrentQueueItemLoaded] CurrentQueueItem пуст, проверяем очередь на наличие трека с QueueOrder = 0"
            );

            // Получаем элемент с QueueOrder = 0 из очереди
            var currentQueueItem = await _queue.GetCurrentQueueItemAsync();

            if (currentQueueItem != null)
            {
                _logger.LogInformation(
                    "[EnsureCurrentQueueItemLoaded] Найден трек с QueueOrder = 0: {TrackName}, загружаем его как текущий",
                    currentQueueItem.Track?.TrackName ?? "null"
                );

                // Устанавливаем его как текущий
                await _stateManager.SetCurrentQueueItemAsync(currentQueueItem, notify: true);

                // Загружаем следующий элемент (QueueOrder = 1)
                await LoadNextQueueItemAsync();

                _logger.LogInformation(
                    "[EnsureCurrentQueueItemLoaded] Текущий трек загружен: {CurrentTrack}, следующий трек: {NextTrack}",
                    currentQueueItem.Track?.TrackName ?? "null",
                    (await _stateManager.GetStateAsync()).NextQueueItem?.Track?.TrackName ?? "null"
                );
            }
            else
            {
                _logger.LogInformation(
                    "[EnsureCurrentQueueItemLoaded] Трек с QueueOrder = 0 не найден в очереди"
                );
            }
        }
        else
        {
            _logger.LogDebug(
                "[EnsureCurrentQueueItemLoaded] CurrentQueueItem уже загружен: {TrackName}",
                currentState.CurrentQueueItem.Track?.TrackName ?? "null"
            );

            // Проверяем, загружен ли NextQueueItem
            if (currentState.NextQueueItem == null)
            {
                _logger.LogInformation(
                    "[EnsureCurrentQueueItemLoaded] NextQueueItem пуст, пытаемся загрузить"
                );
                await LoadNextQueueItemAsync();
            }
        }
    }

    /// <summary>
    /// Уведомить клиентов об изменении очереди
    /// </summary>
    private async Task NotifyQueueChangedAsync()
    {
        var currentQueue = await _queue.GetQueueAsync();
        await _inSignalRHubService.NotifyQueueChangedAsync(currentQueue);
    }

    /// <summary>
    /// Очистить старую историю (элементы с QueueOrder &lt; 0 старше 30 дней)
    /// </summary>
    private async Task CleanupOldHistoryAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            // Удаляем очень старую историю (старше 30 дней)
            var oldHistoryCount = await db
                .SoundRequestQueueItems.Where(qi =>
                    qi.QueueOrder < 0 && qi.RequestedAt < thirtyDaysAgo
                )
                .ExecuteDeleteAsync(_cancellationToken);

            if (oldHistoryCount > 0)
            {
                _logger.LogInformation(
                    "Очистка истории: удалено {HistoryCount} старых элементов (старше 30 дней)",
                    oldHistoryCount
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке истории");
        }
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
            // Проверяем, есть ли будущие треки в очереди (QueueOrder > 0)
            // Текущий трек имеет QueueOrder = 0, поэтому проверяем только > 0
            await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);
            var hasNextTracks = await db
                .SoundRequestQueueItems.AsNoTracking()
                .AnyAsync(qi => qi.QueueOrder > 0, _cancellationToken);

            _logger.LogInformation(
                "Есть ли следующие треки в очереди: {HasNextTracks}",
                hasNextTracks
            );

            // Если нет следующих треков - это был последний трек
            if (!hasNextTracks)
            {
                _logger.LogInformation(
                    "Это был последний трек в очереди, сдвигаем его в историю и останавливаем плеер"
                );

                // Сдвигаем текущий трек в историю (QueueOrder = 0 -> -1)
                await _queue.ShiftQueueAndGetCurrentAsync();

                // Очищаем старую историю
                await CleanupOldHistoryAsync();

                // Останавливаем плеер и обнуляем ссылки
                await _stateManager.StopPlaybackAsync(notify: true);

                // Уведомляем об изменении очереди (она теперь пуста)
                await NotifyQueueChangedAsync();
            }
            else
            {
                // Воспроизводим следующий трек из очереди
                await PlayNextFromQueueAsync();
            }
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

        // Отправляем сообщение в чат Твича об ошибке
        var errorMessage =
            $"⚠️ Трек \"{track.TrackName}\" был пропущен из-за ошибки воспроизведения";
        await _twitchClient.SendMessageToMainTwitchAsync(errorMessage, _logger);

        _logger.LogInformation("Отправлено сообщение в чат Твича об ошибке трека");

        // Проверяем, есть ли будущие треки в очереди (QueueOrder > 0)
        // Текущий трек имеет QueueOrder = 0, поэтому проверяем только > 0
        await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);
        var hasNextTracks = await db
            .SoundRequestQueueItems.AsNoTracking()
            .AnyAsync(qi => qi.QueueOrder > 0, _cancellationToken);

        _logger.LogInformation(
            "Есть ли следующие треки в очереди после ошибки: {HasNextTracks}",
            hasNextTracks
        );

        // Если нет следующих треков - это был последний трек
        if (!hasNextTracks)
        {
            _logger.LogInformation(
                "Это был последний трек в очереди, останавливаем плеер и обнуляем ссылки"
            );

            await _stateManager.StopPlaybackAsync(notify: true);
        }
        else
        {
            // Пытаемся воспроизвести следующий трек
            await PlayNextFromQueueAsync();
        }
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
        var state = GetState();
        var queueCount = (await _queue.GetQueueAsync()).Count;

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

        // Берем историю из QueueItem с QueueOrder < 0
        // -1 = последний проигранный, -2 = предыдущий и т.д.
        var historyItems = await db
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .Where(qi => qi.QueueOrder < 0)
            .OrderByDescending(qi => qi.QueueOrder) // -1, -2, -3...
            .Take(count)
            .ToListAsync(_cancellationToken);

        result = historyItems.Where(qi => qi.Track != null).Select(qi => qi.Track!).ToList();

        return result;
    }

    /// <summary>
    /// Получить историю воспроизведенных треков как QueueItem
    /// </summary>
    public async Task<List<QueueItem>> GetHistoryQueueItemsAsync(int count = 20)
    {
        List<QueueItem> result = [];

        await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);

        result = await db
            .SoundRequestQueueItems.AsNoTracking()
            .Include(qi => qi.Track)
            .Include(qi => qi.RequestedByTwitchUser)
            .Where(qi => qi.QueueOrder < 0)
            .OrderByDescending(qi => qi.QueueOrder) // -1, -2, -3...
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
