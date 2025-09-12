using System.Collections.Concurrent;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelVIPs;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Сервис для получения информации о зрителях канала rxdcodx
/// </summary>
public class RxdcodxViewersService(
    ITwitchAPI api,
    TokenService tokenService,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    ILogger<RxdcodxViewersService> logger
) : BackgroundService, IRxdcodxViewersService
{
    private const string ChannelId = TwitchExstension.ChannelId; // ID канала rxdcodx
    private const string ChannelName = TwitchExstension.Channel;

    // Concurrent кеш для фоловеров
    private readonly ConcurrentDictionary<string, FollowerInfo> _followersCache = new();
    private volatile bool _isCacheInitialized = false;

    /// <summary>
    /// Инициализация кеша фоловеров при запуске сервиса
    /// </summary>
    private async Task InitializeFollowersCacheAsync()
    {
        if (_isCacheInitialized)
        {
            return;
        }

        try
        {
            var followers = await LoadFollowersFromApiAsync();
            if (followers != null)
            {
                _followersCache.Clear();
                foreach (var follower in followers)
                {
                    _followersCache.TryAdd(follower.UserId, follower);
                }
                _isCacheInitialized = true;
            }
        }
        catch (Exception ex)
        {
            // Логируем ошибку, но не прерываем работу сервиса
            logger.LogError(ex, "Ошибка при инициализации кеша фоловеров");
        }
    }

    /// <summary>
    /// Загрузка оригинальных ChannelFollower из API (для обратной совместимости)
    /// </summary>
    private async Task<List<ChannelFollower>?> LoadOriginalChannelFollowersAsync()
    {
        if (tokenService.Token == null)
        {
            return null;
        }

        var pagination = "1";
        var list = new List<ChannelFollower>();

        try
        {
            while (!string.IsNullOrWhiteSpace(pagination))
            {
                pagination = string.Empty;
                var result = await api.Helix.Channels.GetChannelFollowersAsync(
                    ChannelId,
                    null,
                    100,
                    pagination,
                    tokenService.Token.AccessToken
                );

                pagination = result.Pagination?.Cursor ?? string.Empty;
                list.AddRange(result.Data);
            }

            return list;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Ошибка при получении фоловеров канала {ChannelName}",
                ex
            );
        }
    }

    /// <summary>
    /// Загрузка фоловеров из API (внутренний метод)
    /// </summary>
    private async Task<ICollection<FollowerInfo>?> LoadFollowersFromApiAsync()
    {
        if (tokenService.Token == null)
        {
            var startTime = DateTime.Now;

            while (tokenService.Token == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                if (DateTime.Now - startTime > TimeSpan.FromMinutes(1))
                {
                    throw new NullReferenceException(
                        "TwitchAccessToken так и не был инициализирован спустя минуту"
                    );
                }
            }

            if (tokenService.Token is not { AccessToken: not null })
            {
                return null;
            }
        }

        var pagination = "1";
        var list = new HashSet<FollowerInfo>();

        try
        {
            var result2 = await api.Helix.Moderation.GetModeratorsAsync(
                ChannelId,
                null,
                100,
                null,
                tokenService.Token.AccessToken
            );

            var moderators = result2.Data.Select(FollowerInfo.FromModerator);

            foreach (FollowerInfo followerInfo in moderators)
            {
                list.Add(followerInfo);
            }

            var result3 = await api.Helix.Channels.GetVIPsAsync(
                ChannelId,
                null,
                100,
                null,
                tokenService.Token.AccessToken
            );

            var vips = result3.Data.Select(FollowerInfo.FromVip);

            foreach (FollowerInfo followerInfo in vips)
            {
                list.Add(followerInfo);
            }

            while (!string.IsNullOrWhiteSpace(pagination))
            {
                pagination = string.Empty;
                var result = await api.Helix.Channels.GetChannelFollowersAsync(
                    ChannelId,
                    null,
                    100,
                    pagination,
                    tokenService.Token.AccessToken
                );

                pagination = result.Pagination?.Cursor ?? string.Empty;
                var isSameInfo = false;
                var followers = result.Data.Select(FollowerInfo.FromChannelFollower);
                foreach (FollowerInfo followerInfo in followers)
                {
                    var isHaveSameInfo = list.Add(followerInfo);

                    if (!isHaveSameInfo)
                    {
                        var userInfo = list.First(e => e.UserId == followerInfo.UserId);

                        if (userInfo.IsJustFollower)
                        {
                            isSameInfo = true;
                            break;
                        }
                        else
                        {
                            userInfo.FollowedAt = followerInfo.FollowedAt;
                            userInfo.LastUpdated = DateTime.Now;
                        }
                    }
                }

                if (isSameInfo)
                {
                    break;
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Ошибка при получении фоловеров канала {ChannelName}",
                ex
            );
        }
    }

    /// <summary>
    /// Принудительно обновить кеш фоловеров
    /// </summary>
    public async Task RefreshFollowersCacheAsync()
    {
        try
        {
            var followers = await LoadFollowersFromApiAsync();
            if (followers != null)
            {
                _followersCache.Clear();
                foreach (var follower in followers)
                {
                    _followersCache.TryAdd(follower.UserId, follower);
                }
                _isCacheInitialized = true;
                logger.LogInformation("Кеш фоловеров обновлен. Количество: {Count}", followers.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении кеша фоловеров");
        }
    }

    public async Task<ChannelUsersResult?> GetChannelUsersAsync()
    {
        var moderators = await GetModerators();
        var vips = await GetAllViPs();
        var followers = await GetAllFollowers();

        return moderators is not null && vips is not null && followers is { Count: > 0 }
            ? new ChannelUsersResult()
            {
                Followers =
                [
                    .. followers.Where(e =>
                        moderators.All(t => t.UserId != e.UserId)
                        && vips.All(t => t.UserId != e.UserId)
                    ),
                ],
                Moderators = moderators,
                ViPs = vips,
            }
            : null;
    }

    /// <summary>
    /// Получить всех фоловеров канала rxdcodx
    /// </summary>
    /// <returns>Список фоловеров или null если токен недоступен</returns>
    public async Task<List<ChannelFollower>?> GetAllFollowers()
    {
        if (tokenService.Token == null)
        {
            // Если токен недоступен, возвращаем null
            // Используйте GetAllFollowersInfo() для получения данных из кеша
            return null;
        }

        try
        {
            // Пытаемся получить данные из API
            var followers = await LoadFollowersFromApiAsync();
            if (followers != null)
            {
                // Обновляем кеш при успешном получении данных
                _followersCache.Clear();
                foreach (var follower in followers)
                {
                    _followersCache.TryAdd(follower.UserId, follower);
                }
                _isCacheInitialized = true;
                // Возвращаем оригинальные данные из API напрямую
                return await LoadOriginalChannelFollowersAsync();
            }
        }
        catch (Exception ex)
        {
            // При ошибке API возвращаем null для обратной совместимости
            // Используйте GetAllFollowersInfo() для получения данных из кеша
            logger.LogWarning(ex, "API недоступен, GetAllFollowers возвращает null");

            // Если кеш пуст, пробрасываем исключение
            throw new InvalidOperationException(
                $"Ошибка при получении фоловеров канала {ChannelName}",
                ex
            );
        }

        return null;
    }

    /// <summary>
    /// Получить всех VIP канала rxdcodx
    /// </summary>
    /// <returns>Список VIP или null если токен недоступен</returns>
    public async Task<List<ChannelVIPsResponseModel>?> GetAllViPs()
    {
        if (tokenService.Token == null)
        {
            return null;
        }

        var pagination = "1";
        var list = new List<ChannelVIPsResponseModel>();

        try
        {
            while (!string.IsNullOrWhiteSpace(pagination))
            {
                pagination = string.Empty;
                var result = await api.Helix.Channels.GetVIPsAsync(
                    ChannelId,
                    null,
                    100,
                    pagination,
                    tokenService.Token.AccessToken
                );

                pagination = result.Pagination?.Cursor ?? string.Empty;
                list.AddRange(result.Data);
            }

            return list;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Ошибка при получении VIP канала {ChannelName}",
                ex
            );
        }
    }

    /// <summary>
    /// Получить всех модераторов канала rxdcodx
    /// </summary>
    /// <returns>Список модераторов или null если токен недоступен</returns>
    public async Task<List<Moderator>?> GetModerators()
    {
        if (tokenService.Token == null)
        {
            return null;
        }

        var pagination = "1";
        var list = new List<Moderator>();

        try
        {
            while (!string.IsNullOrWhiteSpace(pagination))
            {
                pagination = string.Empty;
                var result = await api.Helix.Moderation.GetModeratorsAsync(
                    ChannelId,
                    null,
                    100,
                    pagination,
                    tokenService.Token.AccessToken
                );

                pagination = result.Pagination?.Cursor ?? string.Empty;
                list.AddRange(result.Data);
            }

            return list;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Ошибка при получении модераторов канала {ChannelName}",
                ex
            );
        }
    }

    /// <summary>
    /// Получить количество фоловеров канала rxdcodx
    /// </summary>
    /// <returns>Количество фоловеров или 0 если токен недоступен</returns>
    public async Task<int> GetFollowersCount()
    {
        var followers = await GetAllFollowers();
        if (followers != null)
        {
            return followers.Count;
        }

        // Если API недоступен, возвращаем количество из кеша
        return _followersCache.Count;
    }

    /// <summary>
    /// Получить количество VIP канала rxdcodx
    /// </summary>
    /// <returns>Количество VIP или 0 если токен недоступен</returns>
    public async Task<int> GetViPsCount()
    {
        var vips = await GetAllViPs();
        return vips?.Count ?? 0;
    }

    /// <summary>
    /// Получить количество модераторов канала rxdcodx
    /// </summary>
    /// <returns>Количество модераторов или 0 если токен недоступен</returns>
    public async Task<int> GetModeratorsCount()
    {
        var moderators = await GetModerators();
        return moderators?.Count ?? 0;
    }

    /// <summary>
    /// Проверить, является ли пользователь фоловером канала rxdcodx
    /// </summary>
    /// <param name="userId">ID пользователя для проверки</param>
    /// <returns>True если пользователь является фоловером</returns>
    public async Task<bool> IsUserFollower(string userId)
    {
        if (tokenService.Token == null)
        {
            // Если токен недоступен, проверяем кеш
            return _followersCache.ContainsKey(userId);
        }

        try
        {
            var result = await api.Helix.Channels.GetChannelFollowersAsync(
                ChannelId,
                userId,
                1,
                null,
                tokenService.Token.AccessToken
            );

            return result.Data.Length != 0;
        }
        catch
        {
            // При ошибке API проверяем кеш
            return _followersCache.ContainsKey(userId);
        }
    }

    /// <summary>
    /// Проверить, является ли пользователь VIP канала rxdcodx
    /// </summary>
    /// <param name="userId">ID пользователя для проверки</param>
    /// <returns>True если пользователь является VIP</returns>
    public async Task<bool> IsUserVip(string userId)
    {
        if (tokenService.Token == null)
        {
            return false;
        }

        try
        {
            var result = await api.Helix.Channels.GetVIPsAsync(
                ChannelId,
                [userId],
                1,
                null,
                tokenService.Token.AccessToken
            );

            return result.Data.Length != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Проверить, является ли пользователь модератором канала rxdcodx
    /// </summary>
    /// <param name="userId">ID пользователя для проверки</param>
    /// <returns>True если пользователь является модератором</returns>
    public async Task<bool> IsUserModerator(string userId)
    {
        if (tokenService.Token == null)
        {
            return false;
        }

        try
        {
            var result = await api.Helix.Moderation.GetModeratorsAsync(
                ChannelId,
                [userId],
                1,
                null,
                tokenService.Token.AccessToken
            );

            return result.Data.Length != 0;
        }
        catch
        {
            return false;
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            // Подписываемся на события
            wsClient.ChannelModeratorAdd += WsClientOnChannelModeratorAdd;
            wsClient.ChannelVipAdd += WsClientOnChannelVipAdd;
            wsClient.ChannelFollow += WsClientOnChannelFollow;

            // Инициализируем кеш фоловеров при запуске
            Task.Factory.StartNew(InitializeFollowersCacheAsync, stoppingToken);
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            wsClient.ChannelModeratorAdd -= WsClientOnChannelModeratorAdd;
            wsClient.ChannelVipAdd -= WsClientOnChannelVipAdd;
            wsClient.ChannelFollow -= WsClientOnChannelFollow;
        });

        return Task.CompletedTask;
    }

    private Task WsClientOnChannelFollow(object sender, ChannelFollowArgs args)
    {
        try
        {
            // Проверяем, что это событие для нашего канала
            var twEvent = args.Notification.Payload.Event;

            if (twEvent.BroadcasterUserId == ChannelId)
            {
                var newFollower = new FollowerInfo
                {
                    UserId = twEvent.UserId,
                    UserName = twEvent.UserName,
                    UserLogin = twEvent.UserLogin,
                    FollowedAt = twEvent.FollowedAt.LocalDateTime,
                    LastUpdated = DateTime.UtcNow,
                };

                // Добавляем нового фоловера в кеш
                if (_followersCache.TryAdd(twEvent.UserId, newFollower))
                {
                    logger.LogInformation(
                        "Добавлен новый фоловер в кеш: {UserName} (ID: {UserId})",
                        twEvent.UserName,
                        twEvent.UserId
                    );
                }
                else
                {
                    // Обновляем существующего фоловера
                    _followersCache.TryUpdate(
                        twEvent.UserId,
                        newFollower,
                        _followersCache[twEvent.UserId]
                    );
                    logger.LogInformation(
                        "Обновлен фоловер в кеше: {UserName} (ID: {UserId})",
                        twEvent.UserName,
                        twEvent.UserId
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении кеша фоловеров");
        }

        return Task.CompletedTask;
    }

    private async Task WsClientOnChannelVipAdd(object sender, ChannelVipArgs args)
    {
        var twEvent = args.Notification.Payload.Event;
        if (twEvent.BroadcasterUserId.Equals(TwitchExstension.ChannelId))
        {
            await Task.Factory.StartNew(() =>
            {
                var newModerator = new FollowerInfo()
                {
                    FollowedAt = DateTime.UnixEpoch,
                    LastUpdated = DateTime.UtcNow,
                    UserId = twEvent.UserId,
                    UserLogin = twEvent.UserLogin,
                    UserName = twEvent.UserName,
                    IsModerator = false,
                    IsVip = true,
                };

                _followersCache.AddOrUpdate(
                    newModerator.UserId,
                    newModerator,
                    (s, info) => newModerator
                );
            });
        }
    }

    private async Task WsClientOnChannelModeratorAdd(object sender, ChannelModeratorArgs args)
    {
        var twEvent = args.Notification.Payload.Event;
        if (twEvent.BroadcasterUserId.Equals(TwitchExstension.ChannelId))
        {
            await Task.Factory.StartNew(() =>
            {
                var newModerator = new FollowerInfo()
                {
                    FollowedAt = DateTime.UnixEpoch,
                    LastUpdated = DateTime.UtcNow,
                    UserId = twEvent.UserId,
                    UserLogin = twEvent.UserLogin,
                    UserName = twEvent.UserName,
                    IsModerator = true,
                    IsVip = false,
                };

                _followersCache.AddOrUpdate(
                    newModerator.UserId,
                    newModerator,
                    (s, info) => newModerator
                );
            });
        }
    }

    /// <summary>
    /// Получить всех фоловеров как FollowerInfo
    /// </summary>
    public async Task<List<FollowerInfo>?> GetAllFollowersInfo()
    {
        if (tokenService.Token == null)
        {
            // Если токен недоступен, возвращаем кеш если он есть
            return !_followersCache.IsEmpty ? [.. _followersCache.Values] : null;
        }

        try
        {
            // Пытаемся получить данные из API
            var followers = await LoadFollowersFromApiAsync();
            if (followers != null)
            {
                // Обновляем кеш при успешном получении данных
                _followersCache.Clear();
                foreach (var follower in followers)
                {
                    _followersCache.TryAdd(follower.UserId, follower);
                }
                _isCacheInitialized = true;
                return [.. followers];
            }
        }
        catch (Exception ex)
        {
            // При ошибке API возвращаем кеш если он есть
            if (!_followersCache.IsEmpty)
            {
                logger.LogWarning(ex, "API недоступен, используем кеш фоловеров");
                return [.. _followersCache.Values];
            }

            // Если кеш пуст, пробрасываем исключение
            throw new InvalidOperationException(
                $"Ошибка при получении фоловеров канала {ChannelName}",
                ex
            );
        }

        return null;
    }

    /// <summary>
    /// Получить информацию о конкретном фоловере
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    public async Task<FollowerInfo?> GetFollowerInfo(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        // Сначала проверяем кеш
        if (_followersCache.TryGetValue(userId, out var cachedFollower))
        {
            return cachedFollower;
        }

        // Если в кеше нет, пытаемся получить из API
        if (tokenService.Token != null)
        {
            try
            {
                var result = await api.Helix.Channels.GetChannelFollowersAsync(
                    ChannelId,
                    userId,
                    1,
                    null,
                    tokenService.Token.AccessToken
                );

                if (result.Data.Length > 0)
                {
                    var followerInfo = FollowerInfo.FromChannelFollower(result.Data[0]);
                    _followersCache.TryAdd(userId, followerInfo);
                    return followerInfo;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при получении информации о фоловере {UserId}", userId);
            }
        }

        return null;
    }

    /// <summary>
    /// Получить количество фоловеров в кеше
    /// </summary>
    public int GetCachedFollowersCount()
    {
        return _followersCache.Count;
    }

    /// <summary>
    /// Очистить кеш фоловеров
    /// </summary>
    public void ClearFollowersCache()
    {
        _followersCache.Clear();
        _isCacheInitialized = false;
        logger.LogInformation("Кеш фоловеров очищен");
    }
}
