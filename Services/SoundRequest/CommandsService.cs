using MARS.Server.ApplicationState;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.SoundRequest.Spotify;
using MARS.Server.Services.SoundRequest.YouTube;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Сервис для работы со звуковыми запросами
/// </summary>
public class CommandsService(
    YouTubeResolver ytResolver,
    SpotifyResolver spotifyResolver,
    SoundRequestUserQueue queue,
    IDbContextFactory<AppDbContext> dbFactory,
    IPlayerController playerController,
    StateManager stateManager,
    InSignalRHubService inSignalRHubService,
    IOptions<SoundRequestConfiguration> soundRequestOptions,
    IOptions<SpotifySoundRequestConfiguration> spotifyOptions
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
            // Проверяем состояние плеера - если остановлен и очередь не пуста, не принимаем новые реквесты
            var playerState = await stateManager.GetStateAsync();
            var queueCount = await queue.GetQueueCountAsync();

            if (playerState.State == PlaybackState.Stopped && queueCount > 0)
            {
                result = "Прием реквестов приостановлен";
                return result;
            }

            // Нормализуем URL - добавляем схему если её нет
            var normalizedQuery = NormalizeUrl(query);

            var provider = await ResolveProviderAsync(cancellationToken);

            // Проверяем, является ли запрос URL
            BaseTrackInfo? info = null;
            if (Uri.TryCreate(normalizedQuery, UriKind.Absolute, out _))
            {
                var sourceTrackId = ExtractSourceTrackId(provider, normalizedQuery);

                // Если удалось извлечь source ID, проверяем БД
                if (!string.IsNullOrWhiteSpace(sourceTrackId))
                {
                    await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                    info = await db
                        .SoundRequestBaseTrackInfos.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.VideoId == sourceTrackId, cancellationToken);
                }

                if (info == null)
                {
                    info =
                        provider == SoundRequestProvider.Spotify
                            ? await spotifyResolver.ResolveTrackAsync(
                                normalizedQuery,
                                cancellationToken
                            )
                            : await ytResolver.ResolveVideoAsync(
                                normalizedQuery,
                                cancellationToken
                            );
                }
            }
            else
            {
                info =
                    provider == SoundRequestProvider.Spotify
                        ? await spotifyResolver.ResolveQueryAsync(query, cancellationToken)
                        : await ytResolver.ResolveQueryAsync(query, cancellationToken);
            }

            if (info != null && user != null)
            {
                // Проверяем длительность трека (максимум 12 минут)
                var maxDuration = TimeSpan.FromMinutes(12);
                if (info.Duration > maxDuration)
                {
                    var durationMinutes = Math.Round(info.Duration.TotalMinutes, 1);
                    result =
                        $"❌ Трек слишком длинный ({durationMinutes} мин). Максимальная длительность: 12 минут";
                    return result;
                }

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
                var waitTime = await CalculateWaitTimeAsync(queueItem.QueueOrder);
                var waitTimeText = FormatWaitTime(waitTime);

                result =
                    $"@{user.DisplayName}, добавлено: {info.Title} [{durationText}]{waitTimeText}";
            }
            else
            {
                result =
                    provider == SoundRequestProvider.Spotify
                        ? "не удалось распознать трек Spotify по запросу"
                        : "не удалось распознать видео по ссылке";
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
    /// Полностью очистить очередь звуковых запросов
    /// </summary>
    public async Task<string> ClearQueueAsync(CancellationToken cancellationToken = default)
    {
        var result = "Очередь уже пуста";

        try
        {
            var queueCount = await queue.GetQueueCountAsync();

            if (queueCount > 0)
            {
                await stateManager.StopPlaybackAsync(notify: true);

                var removedCount = await queue.ClearQueueAsync();
                await NotifyQueueChangedAsync();

                result =
                    removedCount > 0
                        ? $"Очередь очищена, удалено треков: {removedCount}"
                        : "Очередь уже пуста";
            }
        }
        catch (Exception ex)
        {
            result = $"❌ Исключение: {ex.Message}";
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

        var provider = await ResolveProviderAsync(cancellationToken);

        if (provider == SoundRequestProvider.Spotify)
        {
            result = "Плейлисты в Spotify-режиме пока не поддерживаются";
            return result;
        }

        // Проверяем состояние плеера - если остановлен и очередь не пуста, не принимаем новые реквесты
        var playerState = await stateManager.GetStateAsync();
        var queueCount = await queue.GetQueueCountAsync();

        if (playerState.State == PlaybackState.Stopped && queueCount > 0)
        {
            result = "Прием реквестов приостановлен - плеер остановлен";
            return result;
        }

        var items = await ytResolver.ResolvePlaylistAsync(playlistUrl);

        if (items is { Length: > 0 } && user != null)
        {
            // Проверяем состояние плеера ДО добавления плейлиста
            var currentState = await stateManager.GetStateAsync();
            var wasPlayerStopped = currentState.State == PlaybackState.Stopped;
            var queueCountBefore = await queue.GetQueueCountAsync();

            QueueItem? firstQueueItem = null;
            var maxDuration = TimeSpan.FromMinutes(12);
            var skippedTracksCount = 0;

            foreach (var info in items)
            {
                // Проверяем длительность трека (максимум 12 минут)
                if (info.Duration > maxDuration)
                {
                    skippedTracksCount++;
                    continue; // Пропускаем слишком длинные треки
                }

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
                waitTime = await CalculateWaitTimeAsync(firstQueueItem.QueueOrder);
            }
            var waitTimeText = FormatWaitTime(waitTime);

            var addedCount = items.Length - skippedTracksCount;
            result = $"Добавлено треков: {addedCount}";

            if (skippedTracksCount > 0)
            {
                result += $" (пропущено {skippedTracksCount} треков длиннее 12 мин)";
            }

            result += waitTimeText;
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
    /// <returns>Время ожидания в секундах</returns>
    private async Task<TimeSpan> CalculateWaitTimeAsync(int queueOrder)
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
                        if (keyValue is ["v", _])
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
                    || trimmedUrl.Contains("spotify.com", StringComparison.OrdinalIgnoreCase)
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

    private async Task<SoundRequestProvider> ResolveProviderAsync(
        CancellationToken cancellationToken
    )
    {
        var result = soundRequestOptions.Value.Provider;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var providerState = await db
            .RootState.AsNoTracking()
            .SingleOrDefaultAsync(
                s => s.Name == RootStateKeys.SoundRequestProvider,
                cancellationToken
            );

        if (
            providerState is { Value: not null }
            && TryParseProvider(providerState.Value, out var parsedProvider)
        )
        {
            result = parsedProvider;
        }

        if (result == SoundRequestProvider.Spotify && !spotifyOptions.Value.Enabled)
        {
            result = SoundRequestProvider.YouTube;
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

    private string? ExtractSourceTrackId(SoundRequestProvider provider, string normalizedQuery)
    {
        string? result = null;

        if (provider == SoundRequestProvider.Spotify)
        {
            var spotifyTrackId = spotifyResolver.ExtractTrackId(normalizedQuery);
            if (!string.IsNullOrWhiteSpace(spotifyTrackId))
            {
                result = $"spotify:{spotifyTrackId}";
            }
        }
        else
        {
            result = ExtractYouTubeVideoId(normalizedQuery);
        }

        return result;
    }

    /// <summary>
    /// Немедленно воспроизвести трек из очереди
    /// Переместить указанный трек на первую позицию и запустить его
    /// Текущий проигрываемый трек перейдёт в историю
    /// </summary>
    /// <param name="queueItemId">ID элемента очереди для немедленного воспроизведения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сообщение с результатом выполнения</returns>
    public async Task<string> PlayQueueItemNowAsync(
        Guid queueItemId,
        CancellationToken cancellationToken = default
    )
    {
        var result = "❌ Ошибка при выполнении";

        if (queueItemId == Guid.Empty)
        {
            result = "❌ ID трека не может быть пустым";
        }
        else
        {
            try
            {
                // Получаем сам элемент очереди без зависимости от навигации Track
                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                var queueItem = await db
                    .SoundRequestQueueItems.AsNoTracking()
                    .FirstOrDefaultAsync(qi => qi.Id == queueItemId, cancellationToken);

                if (queueItem is null)
                {
                    result = "❌ Трек не найден в очереди";
                }
                else if (queueItem.QueueOrder == 0)
                {
                    result = "❌ Этот трек уже сейчас играет";
                }
                else
                {
                    var track = queueItem.Track;

                    if (track is null)
                    {
                        track = await db.SoundRequestBaseTrackInfos.AsNoTracking().FirstOrDefaultAsync(
                            trackItem => trackItem.Id == queueItem.TrackId,
                            cancellationToken
                        );
                    }

                    if (track is null)
                    {
                        result = "❌ Информация о треке недоступна";
                    }
                    else if (result == "❌ Ошибка при выполнении")
                    {
                        queueItem.Track = track;

                        // Перемещаем элемент на начало очереди и запускаем его
                        var movedItem = await queue.MoveToFrontAndPlayAsync(queueItemId);

                        if (movedItem?.Track is not null && playerController is MainPlayer mainPlayer)
                        {
                            // Запускаем трек на воспроизведение
                            await mainPlayer.PlayAsync(movedItem, cancellationToken);

                            // Уведомляем об изменении очереди
                            await NotifyQueueChangedAsync();

                            result = $"▶️ Сейчас играет: {movedItem.Track!.Title}";
                        }
                        else
                        {
                            result = "❌ Не удалось запустить трек";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result = $"❌ Исключение: {ex.Message}";
            }
        }

        return result;
    }

    /// <summary>
    /// Поменять позицию элемента в очереди
    /// </summary>
    public async Task<string> ReorderQueueItemAsync(
        Guid queueItemId,
        int newPosition,
        CancellationToken cancellationToken = default
    )
    {
        var result = "❌ Ошибка при выполнении";

        if (queueItemId == Guid.Empty)
        {
            return "❌ ID элемента не может быть пустым";
        }

        try
        {
            var moved = await queue.MoveQueueItemToPositionAsync(queueItemId, newPosition);
            if (moved is null)
            {
                result = "❌ Элемент не найден или позиция некорректна";
            }
            else
            {
                await NotifyQueueChangedAsync();
                result = "✅ Позиция обновлена";
            }
        }
        catch (Exception ex)
        {
            result = $"❌ Исключение: {ex.Message}";
        }

        return result;
    }
}
