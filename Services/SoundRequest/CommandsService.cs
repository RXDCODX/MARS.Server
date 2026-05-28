using System.Linq;
using System.Threading;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.SoundRequest.SoundCloud;
using MARS.Server.Services.SoundRequest.Spotify;
using MARS.Server.Services.SoundRequest.YouTube;
using DateTime = System.DateTime;
using Exception = System.Exception;
using Math = System.Math;
using TimeSpan = System.TimeSpan;
using Uri = System.Uri;
using UriKind = System.UriKind;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Сервис для работы со звуковыми запросами
/// </summary>
public class CommandsService(
    YouTubeResolver ytResolver,
    SpotifyResolver spotifyResolver,
    SoundCloudResolver soundCloudResolver,
    SoundRequestUserQueue queue,
    IDbContextFactory<AppDbContext> dbFactory,
    IPlayerController playerController,
    StateManager stateManager,
    InSignalRHubService inSignalRHubService,
    IOptions<SoundRequestConfiguration> soundRequestOptions
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
            var isSoundCloudUrl = IsSoundCloudUrl(normalizedQuery);
            var isSpotifyUrl = IsSpotifyUrl(normalizedQuery);
            var isYouTubeAllowed = IsPlatformAllowed("YouTube");
            var isSpotifyAllowed = IsPlatformAllowed("Spotify");
            var isSoundCloudAllowed = IsPlatformAllowed("SoundCloud");

            if (!isYouTubeAllowed && !isSpotifyAllowed && !isSoundCloudAllowed)
            {
                result = "SoundRequest отключен в конфигурации";
                return result;
            }

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

                if (info == null && isSoundCloudUrl)
                {
                    await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                    if (Uri.TryCreate(normalizedQuery, UriKind.Absolute, out var trackUri))
                    {
                        info = await db
                            .SoundRequestBaseTrackInfos.AsNoTracking()
                            .FirstOrDefaultAsync(t => t.Url == trackUri, cancellationToken);
                    }
                }

                if (info == null)
                {
                    if (isSoundCloudUrl)
                    {
                        if (isSoundCloudAllowed)
                        {
                            info = await soundCloudResolver.ResolveTrackAsync(
                                normalizedQuery,
                                cancellationToken
                            );
                        }
                    }
                    else if (isSpotifyUrl)
                    {
                        if (isSpotifyAllowed)
                        {
                            info = await spotifyResolver.ResolveTrackAsync(
                                normalizedQuery,
                                cancellationToken
                            );
                        }
                    }
                    else if (isYouTubeAllowed)
                    {
                        info = await ytResolver.ResolveVideoAsync(
                            normalizedQuery,
                            cancellationToken
                        );
                    }
                }
            }
            else
            {
                if (provider == SoundRequestProvider.Spotify && isSpotifyAllowed)
                {
                    info = await spotifyResolver.ResolveQueryAsync(query, cancellationToken);
                }
                else if (isYouTubeAllowed)
                {
                    info = await ytResolver.ResolveQueryAsync(query, cancellationToken);
                }
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
                var requestedAt = DateTime.UtcNow;
                var queueItem = await queue.AddToQueueAsync(info, user.TwitchId, user, requestedAt);

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
                if (isSoundCloudUrl)
                {
                    result = isSoundCloudAllowed
                        ? "не удалось распознать трек SoundCloud по ссылке"
                        : "SoundCloud отключен в конфигурации SoundRequest";
                }
                else if (isSpotifyUrl || provider == SoundRequestProvider.Spotify)
                {
                    result = isSpotifyAllowed
                        ? "не удалось распознать трек Spotify по запросу"
                        : "Spotify отключен в конфигурации SoundRequest";
                }
                else
                {
                    result = isYouTubeAllowed
                        ? "не удалось распознать видео по ссылке"
                        : "YouTube отключен в конфигурации SoundRequest";
                }
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
    /// Отменить последнее действие пользователя в очереди (один трек или целый плейлист)
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

            var userQueueItemsQuery = db
                .SoundRequestQueueItems.Include(qi => qi.Track)
                .Where(qi => qi.RequestedByTwitchId == user.TwitchId && qi.QueueOrder >= 0)
                .AsQueryable();

            if (await userQueueItemsQuery.AnyAsync(cancellationToken))
            {
                var lastRequestedAt = await userQueueItemsQuery.MaxAsync(
                    qi => qi.RequestedAt,
                    cancellationToken
                );

                var itemsToCancel = await userQueueItemsQuery
                    .Where(qi => qi.RequestedAt == lastRequestedAt)
                    .OrderByDescending(qi => qi.QueueOrder)
                    .ToListAsync(cancellationToken);

                if (itemsToCancel is not { Count: 0 })
                {
                    foreach (var queueItem in itemsToCancel)
                    {
                        await queue.RemoveFromQueueAsync(queueItem.Id);
                    }

                    await NotifyQueueChangedAsync();

                    if (itemsToCancel.Count == 1 && itemsToCancel[0].Track != null)
                    {
                        result = $"Отменён трек: {itemsToCancel[0].Track?.Title ?? "Пусто"}";
                    }
                    else
                    {
                        result = $"Отменён плейлист из {itemsToCancel.Count} треков";
                    }
                }
                else
                {
                    result = "Нечего отменять";
                }
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
        int maxTracksToAdd = 10,
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

        BaseTrackInfo[]? items = null;

        if (IsSoundCloudUrl(playlistUrl))
        {
            items = await soundCloudResolver.ResolvePlaylistAsync(playlistUrl, cancellationToken);
        }
        else
        {
            items = await ytResolver.ResolvePlaylistAsync(playlistUrl);
        }

        if (items is { Length: > 0 } && user != null)
        {
            // Проверяем состояние плеера ДО добавления плейлиста
            var currentState = await stateManager.GetStateAsync();
            var wasPlayerStopped = currentState.State == PlaybackState.Stopped;
            var queueCountBefore = await queue.GetQueueCountAsync();

            QueueItem? firstQueueItem = null;
            var maxDuration = TimeSpan.FromMinutes(12);
            var skippedTracksCount = 0;
            var addedTracks = 0;
            var effectiveMaxTracksToAdd = maxTracksToAdd > 0 ? maxTracksToAdd : int.MaxValue;
            var requestedAt = DateTime.UtcNow;

            foreach (var info in items)
            {
                // Если уже добавили максимум треков — выходим
                if (addedTracks >= effectiveMaxTracksToAdd)
                {
                    break;
                }

                // Проверяем длительность трека (максимум 12 минут)
                if (info.Duration > maxDuration)
                {
                    skippedTracksCount++;
                    continue; // Пропускаем слишком длинные треки
                }

                // Добавляем трек в очередь
                var queueItem = await queue.AddToQueueAsync(info, user.TwitchId, user, requestedAt);

                // Запоминаем первый элемент плейлиста
                firstQueueItem ??= queueItem;

                addedTracks++;
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

            var addedCount = addedTracks;
            result = $"Добавлено треков: {addedCount}";

            if (skippedTracksCount > 0)
            {
                result += $" (пропущено {skippedTracksCount} треков длиннее 12 мин)";
            }

            // Если в плейлисте было больше подходящих треков, чем разрешено — указываем ограничение
            var possibleAdds = items.Count(i => i.Duration <= maxDuration);
            if (maxTracksToAdd > 0 && possibleAdds > maxTracksToAdd)
            {
                result += $" (ограничено до {maxTracksToAdd} треков)";
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
                var progress = currentState.CurrentTrackProgress.GetValueOrDefault();
                var remainingTicks = currentTrack.Duration.Ticks - progress.Ticks;

                if (remainingTicks > 0)
                {
                    result += TimeSpan.FromTicks(remainingTicks);
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
                var track = queueItem.Track;

                if (track is not null && track.Duration > TimeSpan.Zero)
                {
                    result += track.Duration;
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

        if (result == SoundRequestProvider.Spotify && !IsPlatformAllowed("Spotify"))
        {
            result = SoundRequestProvider.YouTube;
        }

        if (result == SoundRequestProvider.YouTube && !IsPlatformAllowed("YouTube"))
        {
            if (IsPlatformAllowed("Spotify"))
            {
                result = SoundRequestProvider.Spotify;
            }
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

        if (IsSoundCloudUrl(normalizedQuery) && IsPlatformAllowed("SoundCloud"))
        {
            var soundCloudTrackId = ExtractSoundCloudTrackId(normalizedQuery);
            if (!string.IsNullOrWhiteSpace(soundCloudTrackId))
            {
                result = $"soundcloud:{soundCloudTrackId}";
            }
        }
        else if (IsSpotifyUrl(normalizedQuery) && IsPlatformAllowed("Spotify"))
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

    private static bool IsSoundCloudUrl(string url)
    {
        var result = false;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            result =
                uri.Host.Contains("soundcloud.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Contains("snd.sc", StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string? ExtractSoundCloudTrackId(string url)
    {
        string? result = null;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();
            if (host.Contains("soundcloud.com") || host.Contains("snd.sc"))
            {
                result = uri.AbsoluteUri.TrimEnd('/');
            }
        }

        return result;
    }

    private bool IsPlatformAllowed(string platformName)
    {
        var result = false;
        var enabledPlatforms = soundRequestOptions.Value.EnabledPlatforms;

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

    private static bool IsSpotifyUrl(string url)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(url))
        {
            result =
                url.Contains("spotify.com", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("spotify:track:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("spotify:album:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("spotify:playlist:", StringComparison.OrdinalIgnoreCase);
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
                    .Include(queueItem => queueItem.Track)
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
                        track = await db
                            .SoundRequestBaseTrackInfos.AsNoTracking()
                            .FirstOrDefaultAsync(
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

                        if (
                            movedItem?.Track is not null
                            && playerController is MainPlayer mainPlayer
                        )
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
        string result;

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

    /// <summary>
    /// Остановить воспроизведение (очистить текущее состояние плеера)
    /// </summary>
    public async Task<string> StopPlaybackAsync(CancellationToken cancellationToken = default)
    {
        string result;

        try
        {
            await stateManager.StopPlaybackAsync(notify: true);
            await NotifyQueueChangedAsync();
            result = "⏹ Воспроизведение остановлено";
        }
        catch (Exception ex)
        {
            result = $"❌ Исключение: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Возобновить воспроизведение: снять паузу или запустить первый трек из очереди
    /// </summary>
    public async Task<string> ResumePlaybackAsync(CancellationToken cancellationToken = default)
    {
        string result;

        try
        {
            var currentState = await stateManager.GetStateAsync();

            if (currentState.State == PlaybackState.Paused)
            {
                await stateManager.SetPausedAsync(false, notify: true);
                result = "▶️ Воспроизведение возобновлено";
            }
            else if (currentState.State == PlaybackState.Stopped)
            {
                var queueList = await queue.GetQueueAsync();
                var first = queueList.FirstOrDefault(qi => qi.QueueOrder > 0);
                if (first != null && playerController is MainPlayer mainPlayer)
                {
                    await mainPlayer.PlayAsync(first, cancellationToken);
                    await NotifyQueueChangedAsync();
                    result = first.Track != null ? $"▶️ Сейчас играет: {first.Track.Title}" : "▶️ Воспроизведение запущено";
                }
                else
                {
                    result = "Нет треков в очереди";
                }
            }
            else
            {
                result = "Уже воспроизводится";
            }
        }
        catch (Exception ex)
        {
            result = $"❌ Исключение: {ex.Message}";
        }

        return result;
    }
}
