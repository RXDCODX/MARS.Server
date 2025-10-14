using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Фасад для управления системой звуковых запросов
/// Объединяет функциональность плеера, очереди и SignalR уведомлений
/// </summary>
public class SoundRequestManager(
    IPlayerController playerController,
    SoundRequestUserQueue queue,
    IDbContextFactory<AppDbContext> dbFactory,
    IHostApplicationLifetime lifetime
) : IHostedService
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    #region IHostedService Implementation

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Инициализация плеера если он MainPlayer
        if (playerController is MainPlayer mainPlayer)
        {
            mainPlayer.Initialize();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Player Control

    /// <summary>
    /// Возобновить воспроизведение
    /// </summary>
    public async Task ResumeAsync()
    {
        await playerController.ResumeAsync(_cancellationToken);
    }

    /// <summary>
    /// Приостановить воспроизведение
    /// </summary>
    public async Task PauseAsync()
    {
        await playerController.PauseAsync(_cancellationToken);
    }

    /// <summary>
    /// Остановить воспроизведение
    /// </summary>
    public async Task StopAsync()
    {
        await playerController.StopAsync(_cancellationToken);
    }

    /// <summary>
    /// Пропустить текущий трек
    /// </summary>
    public async Task SkipAsync()
    {
        await playerController.SkipAsync(_cancellationToken);
    }

    /// <summary>
    /// Отключить звук
    /// </summary>
    public async Task MuteAsync()
    {
        await playerController.MuteAsync(_cancellationToken);
    }

    /// <summary>
    /// Включить звук
    /// </summary>
    public async Task UnmuteAsync()
    {
        await playerController.UnmuteAsync(_cancellationToken);
    }

    /// <summary>
    /// Установить громкость
    /// </summary>
    public Task SetVolume(int volume)
    {
        return playerController.SetVolumeAsync(volume, _cancellationToken);
    }

    /// <summary>
    /// Получить текущее состояние плеера
    /// </summary>
    public PlayerState GetState()
    {
        return playerController.GetState();
    }

    #endregion

    #region Queue Management

    /// <summary>
    /// Получить очередь треков
    /// </summary>
    public async Task<List<UserRequestedTrack>> GetQueueAsync()
    {
        return await queue.GetQueueAsync();
    }

    /// <summary>
    /// Добавить трек в очередь
    /// </summary>
    public async Task AddTrack(UserRequestedTrack track)
    {
        await queue.AddToQueueAsync(track);
    }

    /// <summary>
    /// Воспроизвести следующий трек из очереди
    /// </summary>
    public async Task PlayNextFromQueueAsync()
    {
        if (playerController is MainPlayer mainPlayer)
        {
            await mainPlayer.PlayNextFromQueueAsync();
        }
    }

    /// <summary>
    /// Воспроизвести конкретный трек из очереди
    /// </summary>
    public async Task PlayTrackFromQueueAsync(Guid trackId)
    {
        var track = (await queue.GetQueueAsync()).FirstOrDefault(t => t.Id == trackId);

        if (track != null && playerController is MainPlayer mainPlayer)
        {
            // Воспроизводим выбранный трек
            await playerController.PlayAsync(
                track.RequestedTrack,
                track.TwitchId,
                track.TwitchDisplayName,
                _cancellationToken
            );

            // Удаляем из очереди
            await queue.RemoveFromQueueAsync(trackId);
        }
    }

    /// <summary>
    /// Удалить трек из очереди
    /// </summary>
    public async Task RemoveTrack(Guid trackId)
    {
        await queue.RemoveFromQueueAsync(trackId);
    }

    #endregion

    #region History

    /// <summary>
    /// Получить историю воспроизведенных треков
    /// </summary>
    public async Task<List<BaseTrackInfo>> GetHistoryAsync(int count = 20)
    {
        List<BaseTrackInfo> result;

        await using var db = await dbFactory.CreateDbContextAsync(_cancellationToken);
        result = await db
            .SoundRequestBaseTrackInfos.AsNoTracking()
            .OrderByDescending(t => t.LastTimePlays)
            .Take(count)
            .ToListAsync(_cancellationToken);

        return result;
    }

    #endregion

    #region Track Events

    /// <summary>
    /// Вызывается когда трек завершил воспроизведение
    /// </summary>
    public async Task OnTrackEnded()
    {
        if (playerController is MainPlayer mainPlayer)
        {
            await mainPlayer.OnTrackEndedAsync();
        }
    }

    /// <summary>
    /// Вызывается когда трек начал воспроизведение
    /// </summary>
    public async Task OnTrackStarted()
    {
        if (playerController is MainPlayer mainPlayer)
        {
            await mainPlayer.OnTrackStartedAsync();
        }
    }

    /// <summary>
    /// Вызывается при ошибке воспроизведения
    /// </summary>
    public async Task OnTrackError()
    {
        if (playerController is MainPlayer mainPlayer)
        {
            await mainPlayer.OnTrackErrorAsync();
        }
    }

    #endregion
}

