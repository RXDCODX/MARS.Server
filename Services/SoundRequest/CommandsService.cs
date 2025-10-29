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
    InSignalRHubService inSignalRHubService
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
        var result = string.Empty;

        if (!string.IsNullOrWhiteSpace(query))
        {
            // Нормализуем URL - добавляем схему если её нет
            var normalizedQuery = NormalizeUrl(query);

            // Проверяем, является ли запрос URL
            BaseTrackInfo? info = null;
            if (Uri.TryCreate(normalizedQuery, UriKind.Absolute, out _))
            {
                // Пытаемся извлечь VideoId из URL
                var videoId = ExtractYouTubeVideoId(normalizedQuery);

                // Если удалось извлечь VideoId, проверяем БД
                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                    info = await db
                        .SoundRequestBaseTrackInfos.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.VideoId == videoId, cancellationToken);
                }

                // Если в БД не нашли, обращаемся к YouTube API
                if (info == null)
                {
                    info = await ytResolver.ResolveVideoAsync(normalizedQuery, cancellationToken);
                }
            }
            else
            {
                // Текстовый запрос — ищем через YouTube Music API
                info = await ytResolver.ResolveQueryAsync(query, cancellationToken);
            }

            if (info != null && user != null)
            {
                // Проверяем состояние плеера ДО добавления в очередь
                var currentState = await stateManager.GetStateAsync();
                var wasPlayerStopped = currentState.State == PlaybackState.Stopped;

                // Проверяем размер очереди ДО добавления
                var queueCountBefore = await queue.GetQueueCountAsync();

                // Добавляем трек в очередь
                var queueItem = await queue.AddToQueueAsync(info, user.TwitchId, user);

                // Пытаемся запустить воспроизведение если нужно
                await TryAutoPlayQueueItemAsync(
                    wasPlayerStopped,
                    queueCountBefore,
                    queueItem,
                    cancellationToken
                );

                var duration = info.Duration;
                var durationText =
                    duration > TimeSpan.Zero
                        ? $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}"
                        : "??:??";

                // Рассчитываем примерное время ожидания
                var waitTime = await CalculateWaitTimeAsync(
                    queueItem.QueueOrder,
                    cancellationToken
                );
                var waitTimeText = FormatWaitTime(waitTime);

                result =
                    $"@{user.DisplayName}, добавлено: {info.Title} [{durationText}]{waitTimeText}";
            }
            else
            {
                result = $"не удалось распознать видео по ссылке";
            }
        }
        else
        {
            result = "Неверные параметры запроса";
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
        var result = string.Empty;

        if (user != null)
        {
            var list = await queue.GetQueueAsync();
            var idx = list.FindIndex(qi => qi.RequestedByTwitchId == user.TwitchId);

            result = idx >= 0 ? $"Ваша позиция в очереди: {idx + 1}" : "Вы не в очереди";
        }
        else
        {
            result = "Не удалось определить пользователя";
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

            var lastQueueItem = await db
                .SoundRequestQueueItems.Include(qi => qi.Track)
                .Where(qi => qi.RequestedByTwitchId == user.TwitchId && qi.QueueOrder >= 0)
                .OrderByDescending(qi => qi.QueueOrder)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastQueueItem is { Track: not null })
            {
                // Удаляем из очереди
                await queue.RemoveFromQueueAsync(lastQueueItem.Id);

                // Уведомляем об изменении очереди
                await NotifyQueueChangedAsync();

                result = $"Отменён трек: {lastQueueItem.Track.Title}";
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

        if (items is { Length: > 0 } && user != null)
        {
            // Проверяем состояние плеера ДО добавления плейлиста
            var currentState = await stateManager.GetStateAsync();
            var wasPlayerStopped = currentState.State == PlaybackState.Stopped;
            var queueCountBefore = await queue.GetQueueCountAsync();

            QueueItem? firstQueueItem = null;

            foreach (var info in items)
            {
                // Добавляем трек в очередь
                var queueItem = await queue.AddToQueueAsync(info, user.TwitchId, user);

                // Запоминаем первый элемент плейлиста
                firstQueueItem ??= queueItem;
            }

            // Если плеер был остановлен И очередь была пуста - запускаем первый трек
            if (
                wasPlayerStopped
                && queueCountBefore == 0
                && firstQueueItem != null
                && playerController is MainPlayer mainPlayer
            )
            {
                await mainPlayer.PlayAsync(firstQueueItem, cancellationToken);
                await queue.RemoveFromQueueAsync(firstQueueItem.Id);
                await NotifyQueueChangedAsync();
            }
            else
            {
                // Если не запускаем автоматически - уведомляем об изменении очереди
                await NotifyQueueChangedAsync();
            }

            // Рассчитываем время ожидания для первого трека плейлиста
            var waitTime = TimeSpan.Zero;
            if (firstQueueItem != null)
            {
                waitTime = await CalculateWaitTimeAsync(
                    firstQueueItem.QueueOrder,
                    cancellationToken
                );
            }
            var waitTimeText = FormatWaitTime(waitTime);

            result = $"Добавлено треков: {items.Length}{waitTimeText}";
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
        await inSignalRHubService.NotifyQueueChangedAsync(currentQueue);
    }

    /// <summary>
    /// Попытаться автоматически запустить воспроизведение элемента очереди, если плеер остановлен и очередь была пуста
    /// </summary>
    private async Task TryAutoPlayQueueItemAsync(
        bool wasPlayerStopped,
        int queueCountBefore,
        QueueItem queueItem,
        CancellationToken cancellationToken
    )
    {
        if (wasPlayerStopped && queueCountBefore == 0 && playerController is MainPlayer mainPlayer)
        {
            await mainPlayer.PlayAsync(queueItem, cancellationToken);
            await queue.RemoveFromQueueAsync(queueItem.Id);
            await NotifyQueueChangedAsync();
        }
        else
        {
            await NotifyQueueChangedAsync();
        }
    }

    /// <summary>
    /// Рассчитать примерное время ожидания до воспроизведения трека
    /// </summary>
    /// <param name="queueOrder">Позиция трека в очереди</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Время ожидания в секундах</returns>
    private async Task<TimeSpan> CalculateWaitTimeAsync(
        int queueOrder,
        CancellationToken cancellationToken = default
    )
    {
        var result = TimeSpan.Zero;

        if (queueOrder > 0)
        {
            // Получаем текущее состояние плеера
            var currentState = await stateManager.GetStateAsync();

            // Если что-то играет, добавляем оставшееся время текущего трека
            if (currentState is { State: PlaybackState.Playing, CurrentQueueItem.Track: not null })
            {
                var currentTrack = currentState.CurrentQueueItem.Track;
                var progress = currentState.CurrentTrackProgress ?? TimeSpan.Zero;
                var remaining = currentTrack.Duration - progress;

                if (remaining > TimeSpan.Zero)
                {
                    result += remaining;
                }
            }

            // Получаем все треки в очереди до нашего трека
            var queueList = await queue.GetQueueAsync();
            var tracksBeforeCurrent = queueList.Where(qi =>
                qi.QueueOrder < queueOrder && qi.Track != null
            );

            // Суммируем длительность всех треков в очереди
            foreach (var queueItem in tracksBeforeCurrent)
            {
                if (queueItem.Track?.Duration > TimeSpan.Zero)
                {
                    result += queueItem.Track.Duration;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Форматировать время ожидания в читаемый текст
    /// </summary>
    /// <param name="waitTime">Время ожидания</param>
    /// <returns>Отформатированная строка</returns>
    private static string FormatWaitTime(TimeSpan waitTime)
    {
        var result = string.Empty;

        if (waitTime > TimeSpan.Zero)
        {
            var totalMinutes = (int)waitTime.TotalMinutes;
            var seconds = waitTime.Seconds;

            if (totalMinutes < 1)
            {
                result = seconds > 0 ? $" через ~ {seconds} сек" : " (меньше секунды)";
            }
            else if (totalMinutes == 1)
            {
                result = seconds > 0 ? $" через ~ 1 мин {seconds} сек" : " через ~ минута";
            }
            else if (totalMinutes < 60)
            {
                result =
                    seconds > 0
                        ? $" через ~ {totalMinutes} мин {seconds} сек"
                        : $" через ~ {totalMinutes} мин";
            }
            else
            {
                var hours = totalMinutes / 60;
                var minutes = totalMinutes % 60;

                if (minutes > 0 && seconds > 0)
                {
                    result = $" через ~ {hours} ч {minutes} мин {seconds} сек";
                }
                else if (minutes > 0)
                {
                    result = $" через ~ {hours} ч {minutes} мин";
                }
                else if (seconds > 0)
                {
                    result = $" через ~ {hours} ч {seconds} сек";
                }
                else
                {
                    result = $" через ~ {hours} ч";
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Извлечь VideoId из YouTube URL
    /// </summary>
    /// <param name="url">URL видео YouTube</param>
    /// <returns>VideoId или null, если не удалось извлечь</returns>
    private static string? ExtractYouTubeVideoId(string url)
    {
        string? result = null;

        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                var uri = new Uri(url);

                // youtube.com/watch?v=VIDEO_ID
                if (
                    uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                    && uri.AbsolutePath.Contains("/watch", StringComparison.OrdinalIgnoreCase)
                )
                {
                    // Парсим query string вручную
                    var query = uri.Query.TrimStart('?');
                    var parameters = query.Split('&');
                    foreach (var param in parameters)
                    {
                        var keyValue = param.Split('=');
                        if (keyValue.Length == 2 && keyValue[0] == "v")
                        {
                            result = keyValue[1];
                            break;
                        }
                    }
                }
                // youtu.be/VIDEO_ID
                else if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
                {
                    result = uri.AbsolutePath.TrimStart('/').Split('/')[0];
                }
                // youtube.com/embed/VIDEO_ID
                else if (
                    uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                    && uri.AbsolutePath.Contains("/embed/", StringComparison.OrdinalIgnoreCase)
                )
                {
                    result = uri.AbsolutePath.Split('/').LastOrDefault();
                }
                // youtube.com/v/VIDEO_ID
                else if (
                    uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                    && uri.AbsolutePath.Contains("/v/", StringComparison.OrdinalIgnoreCase)
                )
                {
                    result = uri.AbsolutePath.Split('/').LastOrDefault();
                }
            }
            catch
            {
                // Игнорируем ошибки парсинга URL
            }
        }

        return result;
    }

    /// <summary>
    /// Нормализовать URL - добавить схему если её нет
    /// </summary>
    /// <param name="url">URL или поисковый запрос</param>
    /// <returns>Нормализованный URL</returns>
    private static string NormalizeUrl(string url)
    {
        var result = url;

        if (!string.IsNullOrWhiteSpace(url))
        {
            var trimmedUrl = url.Trim();

            // Проверяем, начинается ли строка с протокола
            var hasScheme =
                trimmedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || trimmedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            // Если протокола нет, но строка похожа на URL (содержит точку и не содержит пробелов)
            if (
                !hasScheme
                && trimmedUrl.Contains('.')
                && !trimmedUrl.Contains(' ')
                && (
                    trimmedUrl.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                    || trimmedUrl.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                    || trimmedUrl.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                result = $"https://{trimmedUrl}";
            }
            else
            {
                result = trimmedUrl;
            }
        }

        return result;
    }
}
