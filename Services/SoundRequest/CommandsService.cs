using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.SoundRequest.YouTube;
using MARS.Server.Services.Twitch.Entitys;

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
    /// <param name="user">Пользователь Twitch (обязательно)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task<string> AddTrackAsync(
        string query,
        TwitchUser? user,
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

            // Проверяем размер очереди ДО добавления
            var queueCountBefore = await queue.GetQueueCountAsync();

            // Устанавливаем информацию о пользователе
            info.RequestedByTwitchId = user?.TwitchId ?? string.Empty;

            // Добавляем трек в очередь
            await queue.AddToQueueAsync(info);

            // Пытаемся запустить воспроизведение если нужно
            await TryAutoPlayTrackAsync(
                wasPlayerStopped,
                queueCountBefore,
                info,
                user,
                cancellationToken
            );

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
    /// <param name="user">Пользователь Twitch (обязательно)</param>
    public async Task<string> GetUserQueuePositionAsync(TwitchUser? user)
    {
        string result;

        if (user == null)
        {
            result = "Не удалось определить пользователя";
        }
        else
        {
            var list = await queue.GetQueueAsync();
            var idx = list.FindIndex(t => t.RequestedByTwitchId == user.TwitchId);

            if (idx >= 0)
            {
                result = $"Ваша позиция в очереди: {idx + 1}";
            }
            else
            {
                result = "Вы не в очереди";
            }
        }

        return result;
    }

    /// <summary>
    /// Получить информацию о количестве треков перед заказанным треком и примерное время ожидания
    /// </summary>
    /// <param name="user">Пользователь Twitch (обязательно)</param>
    public async Task<string> GetUserQueueDetailsAsync(TwitchUser? user)
    {
        string result;

        if (user == null)
        {
            result = "Не удалось определить пользователя";
        }
        else
        {
            var list = await queue.GetQueueAsync();
            var firstUserTrackIndex = list.FindIndex(t => t.RequestedByTwitchId == user.TwitchId);

            if (firstUserTrackIndex < 0)
            {
                result = "У вас нет треков в очереди";
            }
            else
            {
                // Количество треков перед первым треком пользователя
                var tracksBeforeCount = firstUserTrackIndex;

                // Рассчитываем общую длительность треков перед первым треком пользователя
                var totalWaitTime = TimeSpan.Zero;

                for (var i = 0; i < firstUserTrackIndex; i++)
                {
                    var track = list[i];
                    if (track.Duration > TimeSpan.Zero)
                    {
                        totalWaitTime += track.Duration;
                    }
                }

                // Формируем результат
                if (tracksBeforeCount == 0)
                {
                    result = "Ваш трек следующий в очереди!";
                }
                else
                {
                    var waitTimeText =
                        totalWaitTime > TimeSpan.Zero
                            ? $"{(int)totalWaitTime.TotalMinutes:D2}:{totalWaitTime.Seconds:D2}"
                            : "неизвестно";

                    result =
                        $"Треков в очереди: {tracksBeforeCount}, включим примерно через: ~{waitTimeText}";
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Отменить последний заказанный трек пользователя
    /// </summary>
    /// <param name="user">Пользователь Twitch (обязательно)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task<string> CancelLastTrackAsync(
        TwitchUser? user,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        if (user == null)
        {
            result = "Не удалось определить пользователя";
        }
        else
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var last = await db
                .SoundRequestBaseTrackInfos.Where(t =>
                    !t.IsDeleted && t.QueueOrder != null && t.RequestedByTwitchId == user.TwitchId
                )
                .OrderByDescending(t => t.QueueOrder)
                .FirstOrDefaultAsync(cancellationToken);

            if (last != null)
            {
                // Помечаем как удаленный
                last.IsDeleted = true;
                last.QueueOrder = null;
                db.SoundRequestBaseTrackInfos.Update(last);
                await db.SaveChangesAsync(cancellationToken);

                // Уведомляем об изменении очереди
                await NotifyQueueChangedAsync();

                result = "Последний заказ удален";
            }
            else
            {
                result = "Нечего отменять";
            }
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
        TwitchUser? user,
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

                // Устанавливаем информацию о пользователе
                trackToAdd.RequestedByTwitchId = user?.TwitchId ?? string.Empty;

                await queue.AddToQueueAsync(trackToAdd);

                // Запоминаем первый трек плейлиста
                firstTrack ??= trackToAdd;
            }

            // Если плеер был остановлен И очередь была пуста - запускаем первый трек
            if (wasPlayerStopped && queueCountBefore == 0 && firstTrack != null)
            {
                await playerController.PlayAsync(firstTrack, user, cancellationToken);
                await queue.RemoveFromQueueAsync(firstTrack.Id);
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

    /// <summary>
    /// Попытаться автоматически запустить воспроизведение трека, если плеер остановлен и очередь была пуста
    /// </summary>
    private async Task TryAutoPlayTrackAsync(
        bool wasPlayerStopped,
        int queueCountBefore,
        BaseTrackInfo track,
        TwitchUser? user,
        CancellationToken cancellationToken
    )
    {
        if (wasPlayerStopped && queueCountBefore == 0)
        {
            await playerController.PlayAsync(track, user, cancellationToken);
            await queue.RemoveFromQueueAsync(track.Id);
            await NotifyQueueChangedAsync();
        }
        else
        {
            await NotifyQueueChangedAsync();
        }
    }
}
