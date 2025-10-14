using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.SoundRequest.YouTube;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Сервис для работы со звуковыми запросами
/// </summary>
public class CommandsService(
    YouTubeResolver ytResolver,
    SoundRequestUserQueue queue,
    IDbContextFactory<AppDbContext> dbFactory,
    IPlayerController playerController,
    StateManager stateManager,
    SignalRService signalRService
)
{
    /// <summary>
    /// Добавить трек в очередь по URL или поисковому запросу
    /// </summary>
    /// <param name="query">URL видео или поисковый запрос (обязательно)</param>
    /// <param name="userId">ID пользователя Twitch (обязательно)</param>
    /// <param name="displayName">Отображаемое имя пользователя (обязательно)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task<string> AddTrackAsync(
        string query,
        string userId,
        string displayName,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        BaseTrackInfo? info;

        // Проверяем, является ли запрос URL
        if (Uri.TryCreate(query, UriKind.Absolute, out _))
        {
            info = await ytResolver.ResolveVideoAsync(query, cancellationToken);
        }
        else
        {
            // Текстовый запрос — ищем через YouTube Music API
            info = await ytResolver.ResolveQueryAsync(query, cancellationToken);
        }

        if (info != null)
        {
            // Проверяем состояние плеера ДО добавления в очередь
            var currentState = await stateManager.GetStateAsync();
            var wasPlayerStopped = currentState.IsStoped;

            // Сохраняем трек в базу данных или загружаем существующий
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var existingTrack = await db
                .SoundRequestBaseTrackInfos.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Url == info.Url, cancellationToken);

            if (existingTrack != null)
            {
                // Используем существующий трек вместо нового
                info = existingTrack;
            }
            else
            {
                // Трек новый, сохраняем его
                db.SoundRequestBaseTrackInfos.Add(info);
                await db.SaveChangesAsync(cancellationToken);
            }

            // Проверяем размер очереди ДО добавления
            var queueCountBefore = await queue.GetQueueCountAsync();

            // Добавляем трек в очередь
            await queue.AddToQueueAsync(
                new UserRequestedTrack
                {
                    RequestedTrack = info,
                    RequestedTrackId = info.Id,
                    TwitchId = userId,
                    TwitchDisplayName = displayName,
                }
            );

            // Если плеер был остановлен И очередь была пуста - запускаем воспроизведение
            if (wasPlayerStopped && queueCountBefore == 0)
            {
                await playerController.PlayAsync(info, userId, displayName, cancellationToken);
                var addedTrack = (await queue.GetQueueAsync()).First(t =>
                    t.RequestedTrackId == info.Id
                );
                await queue.RemoveFromQueueAsync(addedTrack.Id);
                await NotifyQueueChangedAsync();
            }
            else
            {
                // Если не запускаем автоматически - уведомляем об изменении очереди
                await NotifyQueueChangedAsync();
            }

            var duration = info.Duration;
            var durationText =
                duration > TimeSpan.Zero
                    ? $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}"
                    : "??:??";

            result = $"Добавлено: {info.Title} [{durationText}]";
        }
        else
        {
            result = "Не удалось распознать видео по ссылке";
        }

        return result;
    }

    /// <summary>
    /// Получить текущую или последнюю проигранную песню
    /// </summary>
    public async Task<string> GetCurrentSongAsync(CancellationToken cancellationToken = default)
    {
        string result;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var last = await db
            .SoundRequestBaseTrackInfos.AsNoTracking()
            .OrderByDescending(t => t.LastTimePlays)
            .FirstOrDefaultAsync(cancellationToken);

        if (last != null)
        {
            result = $"Сейчас: {last.Title}";
        }
        else
        {
            result = "Нет информации о текущей песне";
        }

        return result;
    }

    /// <summary>
    /// Получить позицию пользователя в очереди
    /// </summary>
    /// <param name="userId">ID пользователя Twitch (обязательно)</param>
    public async Task<string> GetUserQueuePositionAsync(string userId)
    {
        string result;

        var list = await queue.GetQueueAsync();
        var idx = list.FindIndex(t => t.TwitchId == userId);

        if (idx >= 0)
        {
            result = $"Ваша позиция в очереди: {idx + 1}";
        }
        else
        {
            result = "Вы не в очереди";
        }

        return result;
    }

    /// <summary>
    /// Отменить последний заказанный трек пользователя
    /// </summary>
    /// <param name="userId">ID пользователя Twitch (обязательно)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task<string> CancelLastTrackAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var last = await db
            .SoundRequestUserQueue.Where(t => t.TwitchId == userId)
            .OrderByDescending(t => t.Order)
            .FirstOrDefaultAsync(cancellationToken);

        if (last != null)
        {
            db.SoundRequestUserQueue.Remove(last);
            await db.SaveChangesAsync(cancellationToken);

            // Уведомляем об изменении очереди
            await NotifyQueueChangedAsync();

            result = "Последний заказ удален";
        }
        else
        {
            result = "Нечего отменять";
        }

        return result;
    }

    /// <summary>
    /// Добавить весь плейлист в очередь
    /// </summary>
    /// <param name="playlistUrl">URL плейлиста YouTube (обязательно)</param>
    /// <param name="userId">ID пользователя Twitch (обязательно)</param>
    /// <param name="displayName">Отображаемое имя пользователя (обязательно)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task<string> AddPlaylistAsync(
        string playlistUrl,
        string userId,
        string displayName,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        var items = await ytResolver.ResolvePlaylistAsync(playlistUrl);

        if (items is { Length: > 0 })
        {
            // Проверяем состояние плеера ДО добавления плейлиста
            var currentState = await stateManager.GetStateAsync();
            var wasPlayerStopped = currentState.IsStoped;
            var queueCountBefore = await queue.GetQueueCountAsync();

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            BaseTrackInfo? firstTrack = null;

            foreach (var info in items)
            {
                // Проверяем существование трека и загружаем его, если есть
                var existingTrack = await db
                    .SoundRequestBaseTrackInfos.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Url == info.Url, cancellationToken);

                var trackToAdd = info;
                if (existingTrack != null)
                {
                    // Используем существующий трек
                    trackToAdd = existingTrack;
                }
                else
                {
                    // Трек новый, сохраняем его
                    db.SoundRequestBaseTrackInfos.Add(info);
                    await db.SaveChangesAsync(cancellationToken);
                }

                await queue.AddToQueueAsync(
                    new UserRequestedTrack
                    {
                        RequestedTrack = trackToAdd,
                        RequestedTrackId = trackToAdd.Id,
                        TwitchId = userId,
                        TwitchDisplayName = displayName,
                    }
                );

                // Запоминаем первый трек плейлиста
                firstTrack ??= trackToAdd;
            }

            // Если плеер был остановлен И очередь была пуста - запускаем первый трек
            if (wasPlayerStopped && queueCountBefore == 0 && firstTrack != null)
            {
                await playerController.PlayAsync(
                    firstTrack,
                    userId,
                    displayName,
                    cancellationToken
                );
                var addedTrack = (await queue.GetQueueAsync()).First(t =>
                    t.RequestedTrackId == firstTrack.Id
                );
                await queue.RemoveFromQueueAsync(addedTrack.Id);
                await NotifyQueueChangedAsync();
            }
            else
            {
                // Если не запускаем автоматически - уведомляем об изменении очереди
                await NotifyQueueChangedAsync();
            }

            result = $"Добавлено треков: {items.Length}";
        }
        else
        {
            result = "Не удалось прочитать плейлист";
        }

        return result;
    }

    /// <summary>
    /// Уведомить клиентов об изменении очереди
    /// </summary>
    private async Task NotifyQueueChangedAsync()
    {
        var currentQueue = await queue.GetQueueAsync();
        await signalRService.NotifyQueueChangedAsync(currentQueue);
    }
}

