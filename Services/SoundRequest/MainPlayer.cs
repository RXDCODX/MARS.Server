using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.ApplicationState;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.SoundRequest.Spotify;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.DynamicLinq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Основной контроллер аудиоплеера с интеграцией всех сервисов
/// </summary>
public class MainPlayer : IPlayerController, IHostedService, IDisposable
{
    private const int MaxHistoryEntries = 1000;
    private readonly CancellationToken _cancellationToken;
    private bool _disposed;
    private readonly StateManager _stateManager;
    private readonly InSignalRHubService _inSignalRHubService;
    private readonly SoundRequestUserQueue _queue;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<MainPlayer> _logger;
    private readonly SpotifyPlaybackService _spotifyPlaybackService;
    private readonly SoundRequestConfiguration _soundRequestConfiguration;
    private readonly SpotifySoundRequestConfiguration _spotifyConfiguration;
    private readonly SemaphoreSlim _spotifyMonitorTransitionLock = new(1, 1);
    private Task? _spotifyMonitorTask;
    private Guid? _lastSpotifyCompletedQueueItemId;
    private DateTime _lastSpotifyTrackPlayIssuedAtUtc = DateTime.UnixEpoch;

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
        SpotifyPlaybackService spotifyPlaybackService,
        IOptions<SoundRequestConfiguration> soundRequestOptions,
        IOptions<SpotifySoundRequestConfiguration> spotifyOptions,
        ITwitchClient twitchClient,
        ILogger<MainPlayer> logger
    )
    {
        _stateManager = stateManager;
        _inSignalRHubService = inSignalRHubService;
        _queue = queue;
        _dbFactory = dbFactory;
        _spotifyPlaybackService = spotifyPlaybackService;
        _soundRequestConfiguration = soundRequestOptions.Value;
        _spotifyConfiguration = spotifyOptions.Value;
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

        if (await IsSpotifyModeAsync(_cancellationToken))
        {
            _spotifyMonitorTask = Task.Run(
                () => MonitorSpotifyPlaybackAsync(_cancellationToken),
                _cancellationToken
            );
        }
    }

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        if (_spotifyMonitorTask != null)
        {
            try
            {
                await _spotifyMonitorTask;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Spotify playback monitor stopped with exception");
            }
        }
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
            "[InitializeAsync] Текущее состояние: State={State}, CurrentQueueItem={CurrentQueueItem}, QueueCount={QueueCount}",
            currentState.State,
            currentState.CurrentQueueItem?.Track?.TrackName ?? "null",
            queueCount
        );

        if (queueCount > 0)
        {
            _logger.LogInformation(
                "[InitializeAsync] Следующий элемент очереди не установлен, но в очереди есть {QueueCount} элементов, загружаем...",
                queueCount
            );
            var updatedState = await _stateManager.GetStateAsync();
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

            if (
                await IsSpotifyModeAsync(_cancellationToken)
                && _spotifyPlaybackService.IsConfigured()
            )
            {
                var started = await _spotifyPlaybackService.PlayTrackAsync(
                    queueItem.Track,
                    _cancellationToken
                );
                if (!started)
                {
                    throw new InvalidOperationException(
                        "Не удалось запустить трек в Spotify клиенте"
                    );
                }

                _lastSpotifyTrackPlayIssuedAtUtc = DateTime.UtcNow;
            }

            _logger.LogDebug("Состояние обновлено, уведомление отправлено");

            // Обновляем время последнего воспроизведения в БД
            await UpdateQueueItemLastPlayedAsync(queueItem);

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
        if (await IsSpotifyModeAsync(ct) && _spotifyPlaybackService.IsConfigured())
        {
            await _spotifyPlaybackService.PauseAsync(ct);
        }

        await _stateManager.SetPausedAsync(true, notify: true);
    }

    /// <summary>
    /// Возобновить воспроизведение
    /// </summary>
    public async Task ResumeAsync(CancellationToken ct)
    {
        if (await IsSpotifyModeAsync(ct) && _spotifyPlaybackService.IsConfigured())
        {
            await _spotifyPlaybackService.ResumeAsync(ct);
        }

        await _stateManager.SetPausedAsync(false, notify: true);
    }

    /// <summary>
    /// Остановить воспроизведение
    /// </summary>
    public async Task StopAsync(CancellationToken ct)
    {
        if (await IsSpotifyModeAsync(ct) && _spotifyPlaybackService.IsConfigured())
        {
            await _spotifyPlaybackService.StopAsync(ct);
        }

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
        if (await IsSpotifyModeAsync(ct) && _spotifyPlaybackService.IsConfigured())
        {
            await _spotifyPlaybackService.SetVolumeAsync((int)Math.Clamp(volume, 0f, 100f), ct);
        }

        await _stateManager.SetVolumeAsync(volume, notify: true);
    }

    /// <summary>
    /// Отключить звук
    /// </summary>
    public async Task MuteAsync(CancellationToken ct)
    {
        if (await IsSpotifyModeAsync(ct) && _spotifyPlaybackService.IsConfigured())
        {
            await _spotifyPlaybackService.SetVolumeAsync(0, ct);
        }

        await _stateManager.SetMutedAsync(true, notify: true);
    }

    /// <summary>
    /// Включить звук
    /// </summary>
    public async Task UnmuteAsync(CancellationToken ct)
    {
        if (await IsSpotifyModeAsync(ct) && _spotifyPlaybackService.IsConfigured())
        {
            var state = await _stateManager.GetStateAsync();
            await _spotifyPlaybackService.SetVolumeAsync(
                (int)Math.Clamp(state.Volume, 0f, 100f),
                ct
            );
        }

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
            try
            {
                await db.SoundRequestQueueItems.ExecuteUpdateAsync(
                    e => e.SetProperty(t => t.QueueOrder, t => t.QueueOrder + 1),
                    cancellationToken: _cancellationToken
                );
            }
            catch (InvalidOperationException)
            {
                var queueItems = await db.SoundRequestQueueItems.ToListAsync(
                    cancellationToken: _cancellationToken
                );

                foreach (var queueItem in queueItems)
                {
                    queueItem.QueueOrder += 1;
                }

                await db.SaveChangesAsync(_cancellationToken);
            }

            await PlayAsync(previousQueueItem, _cancellationToken);
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

                _logger.LogInformation(
                    "[EnsureCurrentQueueItemLoaded] Текущий трек загружен: {CurrentTrack}",
                    currentQueueItem.Track?.TrackName ?? "null"
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

            _logger.LogDebug("[EnsureCurrentQueueItemLoaded] Текущий трек уже загружен");
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
    /// Ограничить историю воспроизведений до MaxHistoryEntries записей
    /// </summary>
    private async Task CleanupOldHistoryAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(_cancellationToken);

            var historyQuery = db.SoundRequestQueueItems.Where(qi =>
                qi.QueueOrder < 0
                && !db.SoundRequestPlayerState.Any(ps => ps.CurrentQueueItemId == qi.Id)
            );

            var historyCount = await historyQuery.CountAsync(_cancellationToken);
            var historyToDeleteCount = Math.Max(0, historyCount - MaxHistoryEntries);

            var oldHistoryCount = 0;

            if (historyToDeleteCount > 0)
            {
                try
                {
                    oldHistoryCount = await historyQuery
                        .OrderBy(qi => qi.QueueOrder)
                        .Take(historyToDeleteCount)
                        .ExecuteDeleteAsync(_cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    var oldHistoryItems = await historyQuery
                        .OrderBy(qi => qi.QueueOrder)
                        .Take(historyToDeleteCount)
                        .ToListAsync(_cancellationToken);

                    if (oldHistoryItems.Count > 0)
                    {
                        db.SoundRequestQueueItems.RemoveRange(oldHistoryItems);
                        oldHistoryCount = oldHistoryItems.Count;
                        await db.SaveChangesAsync(_cancellationToken);
                    }
                }
            }

            if (oldHistoryCount > 0)
            {
                _logger.LogInformation(
                    "Очистка истории: удалено {HistoryCount} элементов, лимит истории = {HistoryLimit}",
                    oldHistoryCount,
                    MaxHistoryEntries
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
        var currentTrackId = currentState.CurrentQueueItem?.Track?.Id;
        var isEndedForCurrentTrack = currentTrackId.HasValue && currentTrackId.Value == track.Id;

        _logger.LogInformation(
            "Обработка завершения трека: {TrackName} (ID: {TrackId})",
            track.TrackName,
            track.Id
        );

        // Переключаем очередь, если завершился именно текущий трек и плеер не остановлен.
        // Это защищает от гонки, когда FrontStateChange успел выставить Paused перед Ended.
        if (isEndedForCurrentTrack && currentState.State != PlaybackState.Stopped)
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
            if (queueItem.QueueOrder != 0)
            {
                var movedQueueItem = await _queue.MoveToFrontAndPlayAsync(queueItemId);

                if (movedQueueItem != null)
                {
                    await PlayAsync(movedQueueItem, _cancellationToken);
                }
            }

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

    #region Spotify Monitoring

    private async Task MonitorSpotifyPlaybackAsync(CancellationToken ct)
    {
        var pollingInterval = Math.Max(750, _spotifyConfiguration.PollingIntervalMs);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_spotifyPlaybackService.IsConfigured())
                {
                    await HandleSpotifyPlaybackTickAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ошибка Spotify playback monitor tick");
            }

            try
            {
                await Task.Delay(pollingInterval, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task HandleSpotifyPlaybackTickAsync(CancellationToken ct)
    {
        var state = await _stateManager.GetStateAsync();
        var currentQueueItem = state.CurrentQueueItem;

        if (
            state.State == PlaybackState.Playing
            && currentQueueItem?.Track != null
            && _spotifyPlaybackService.IsSpotifyTrack(currentQueueItem.Track)
        )
        {
            var playback = await _spotifyPlaybackService.GetCurrentPlaybackAsync(ct);

            if (playback != null)
            {
                await _stateManager.UpdateCurrentTrackProgressAsync(
                    TimeSpan.FromMilliseconds(Math.Max(0, playback.ProgressMs)),
                    notify: false
                );

                var expectedTrackId = _spotifyPlaybackService.GetSpotifyTrackId(
                    currentQueueItem.Track
                );
                var isTrackChangedExternally =
                    !string.IsNullOrWhiteSpace(playback.TrackId)
                    && !string.IsNullOrWhiteSpace(expectedTrackId)
                    && !string.Equals(
                        playback.TrackId,
                        expectedTrackId,
                        StringComparison.OrdinalIgnoreCase
                    );

                var isTrackAlmostEnded =
                    playback is { IsPlaying: false, DurationMs: > 0 }
                    && playback.ProgressMs >= playback.DurationMs - 1200;

                var graceMs = Math.Max(0, _spotifyConfiguration.UserPlaybackPriorityGraceMs);
                var isInsidePriorityGraceWindow =
                    graceMs > 0
                    && DateTime.UtcNow - _lastSpotifyTrackPlayIssuedAtUtc
                        < TimeSpan.FromMilliseconds(graceMs);

                if (
                    _spotifyConfiguration.PrioritizeUserPlayback
                    && isTrackChangedExternally
                    && playback.IsPlaying
                    && !isInsidePriorityGraceWindow
                )
                {
                    _logger.LogInformation(
                        "Обнаружено ручное воспроизведение в Spotify (TrackId={TrackId}) - SoundRequest поставлен на паузу",
                        playback.TrackId ?? "null"
                    );

                    await _stateManager.SetPlaybackStateAsync(PlaybackState.Paused, notify: true);
                    _lastSpotifyCompletedQueueItemId = null;
                }
                else if (isTrackChangedExternally || isTrackAlmostEnded)
                {
                    await _spotifyMonitorTransitionLock.WaitAsync(ct);
                    try
                    {
                        if (_lastSpotifyCompletedQueueItemId != currentQueueItem.Id)
                        {
                            _lastSpotifyCompletedQueueItemId = currentQueueItem.Id;
                            await OnTrackEndedAsync(currentQueueItem.Track);
                        }
                    }
                    finally
                    {
                        _spotifyMonitorTransitionLock.Release();
                    }
                }
            }
        }
        else
        {
            _lastSpotifyCompletedQueueItemId = null;
        }
    }

    private async Task<bool> IsSpotifyModeAsync(CancellationToken ct)
    {
        var provider = _soundRequestConfiguration.Provider;
        var result = false;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var providerState = await db
            .RootState.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Name == RootStateKeys.SoundRequestProvider, ct);

        if (
            providerState is { Value: not null }
            && TryParseProvider(providerState.Value, out var parsedProvider)
        )
        {
            provider = parsedProvider;
        }

        if (
            provider == SoundRequestProvider.Spotify
            && _spotifyConfiguration.Enabled
            && IsPlatformAllowed("Spotify")
        )
        {
            result = true;
        }

        return result;
    }

    private static bool TryParseProvider(string rawValue, out SoundRequestProvider provider)
    {
        var result = false;
        provider = SoundRequestProvider.YouTube;

        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            var normalizedValue = rawValue.Trim();
            if (Enum.TryParse<SoundRequestProvider>(normalizedValue, true, out var byName))
            {
                provider = byName;
                result = true;
            }
            else if (int.TryParse(normalizedValue, out var numericValue))
            {
                if (Enum.IsDefined(typeof(SoundRequestProvider), numericValue))
                {
                    provider = (SoundRequestProvider)numericValue;
                    result = true;
                }
            }
        }

        return result;
    }

    private bool IsPlatformAllowed(string platformName)
    {
        var result = false;
        var enabledPlatforms = _soundRequestConfiguration.EnabledPlatforms;

        if (enabledPlatforms.Length > 0)
        {
            foreach (var enabledPlatform in enabledPlatforms)
            {
                if (enabledPlatform.Trim().Equals(platformName, StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    break;
                }
            }
        }

        return result;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_disposed)
        {
            _spotifyMonitorTransitionLock.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}
