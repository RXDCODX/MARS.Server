using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.SoundRequest.YouTube;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Сервис для работы со звуковыми запросами
/// </summary>
public class CommandsService(
    YouTubeResolver ytResolver,
    SoundRequestUserQueue queue,
    IDbContextFactory<AppDbContext> dbFactory
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
            // Сохраняем трек в базу данных, если его там ещё нет
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var exists = await db
                .SoundRequestBaseTrackInfos.AsNoTracking()
                .AnyAsync(t => t.Url == info.Url, cancellationToken);

            if (!exists)
            {
                db.SoundRequestBaseTrackInfos.Add(info);
                await db.SaveChangesAsync(cancellationToken);
            }

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
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            foreach (var info in items)
            {
                var exists = await db
                    .SoundRequestBaseTrackInfos.AsNoTracking()
                    .AnyAsync(t => t.Url == info.Url, cancellationToken);

                if (!exists)
                {
                    db.SoundRequestBaseTrackInfos.Add(info);
                    await db.SaveChangesAsync(cancellationToken);
                }

                await queue.AddToQueueAsync(
                    new UserRequestedTrack
                    {
                        RequestedTrack = info,
                        RequestedTrackId = info.Id,
                        TwitchId = userId,
                        TwitchDisplayName = displayName,
                    }
                );
            }

            result = $"Добавлено треков: {items.Length}";
        }
        else
        {
            result = "Не удалось прочитать плейлист";
        }

        return result;
    }
}
