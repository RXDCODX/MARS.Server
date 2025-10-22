using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Основной контроллер аудиоплеера с интеграцией всех сервисов
/// </summary>
public class MainPlayer : IPlayerController, IDisposable
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
    }

    #endregion

    #region IPlayerController Implementation

    /// <summary>
    /// Начать воспроизведение трека с информацией о пользователе, заказавшем трек
    /// </summary>
    public async Task PlayAsync(BaseTrackInfo track, TwitchUser? user, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Начинаем воспроизведение трека: {TrackName}, URL: {Url}",
                track.TrackName,
                track.Url
            );

            // Обновляем состояние - начинаем воспроизведение
            await _stateManager.StartPlayingAsync(track, user, notify: true);

            _logger.LogDebug("Состояние обновлено, уведомление отправлено");

            // Обновляем время последнего воспроизведения в БД
            await UpdateTrackLastPlayedAsync(track);

            // Загружаем следующий трек из очереди
            await LoadNextTrackAsync();

            _logger.LogInformation("Трек успешно запущен: {TrackName}", track.TrackName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при воспроизведении трека: {TrackName}", track.TrackName);
            // При ошибке вызываем событие ошибки
            await OutSignalRHubServiceOnError(track);
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
        _logger.LogInformation("Пропуск текущего трека");

        // Воспроизводим следующий трек из очереди
        await PlayNextFromQueueAsync();
    }

    /// <summary>
    /// Установить громкость
    /// </summary>
    public async Task SetVolumeAsync(int volume, CancellationToken ct)
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
    /// Получить текущее состояние плеера
    /// </summary>
    public PlayerState GetState()
    {
        return _stateManager.GetState();
    }

    #endregion

    #region Queue Management

    /// <summary>
    /// Воспроизвести следующий трек из очереди
    /// </summary>
    public async Task PlayNextFromQueueAsync()
    {
        var nextTrack = await _queue.GetNextTrackAsync();

        _logger.LogDebug(
            "Следующий трек из очереди: {TrackName}",
            nextTrack != null ? nextTrack.TrackName : "null"
        );

        if (nextTrack != null)
        {
            _logger.LogInformation(
                "Начинаем воспроизведение следующего трека: {TrackName}",
                nextTrack.TrackName
            );

            // Воспроизводим трек с информацией о пользователе
            await PlayAsync(nextTrack, nextTrack.RequestedByTwitchUser, _cancellationToken);

            // Удаляем из очереди
            await _queue.RemoveFromQueueAsync(nextTrack.Id);

            // Уведомляем об изменении очереди
            await NotifyQueueChangedAsync();

            _logger.LogInformation(
                "Трек из очереди успешно запущен: {TrackName}",
                nextTrack.TrackName
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
    /// Загрузить информацию о следующем треке в состояние
    /// </summary>
    private async Task LoadNextTrackAsync()
    {
        var nextTrack = await _queue.GetNextTrackAsync();
        await _stateManager.SetNextTrackAsync(nextTrack, notify: true);
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

        // Обновляем время последнего воспроизведения в БД
        await UpdateTrackLastPlayedAsync(track);

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
    /// Обновить время последнего воспроизведения трека в базе данных
    /// </summary>
    private async Task UpdateTrackLastPlayedAsync(BaseTrackInfo track)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);

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
