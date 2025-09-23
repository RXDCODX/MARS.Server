using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

// ReSharper disable All

namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Сервис для получения информации о зрителях канала rxdcodx
/// </summary>
public class RxdcodxViewersService(
    ITwitchAPI api,
    TokenService tokenService,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    ILogger<RxdcodxViewersService> logger,
    FollowerDbService followerDbService,
    TwitchUserInfoService userInfoService
) : BackgroundService, IRxdcodxViewersService
{
    private const string ChannelId = TwitchExstension.ChannelId; // ID канала rxdcodx
    private const string ChannelName = TwitchExstension.Channel;

    /// <summary>
    /// Инициализация кеша фоловеров при запуске сервиса
    /// </summary>
    private async Task InitializeFollowersCacheAsync()
    {
        try
        {
            // Проверяем, есть ли данные в базе данных
            var followersFromDb = await followerDbService.GetAllFollowersFromDbAsync();

            if (followersFromDb.Count != 0)
            {
                logger.LogInformation(
                    "Кеш фоловеров инициализирован из базы данных: {Count} фоловеров",
                    followersFromDb.Count
                );

                // Обновляем данные из API в фоновом режиме
                _ = Task.Run(UpdateFollowersFromApiAsync);
            }
            else
            {
                // Если в БД нет данных, загружаем из API
                var followers = await LoadFollowersFromApiAsync();
                if (followers != null)
                {
                    // Обогащаем данные дополнительной информацией
                    var enrichedFollowers = await userInfoService.EnrichFollowersInfoAsync(
                        followers
                    );

                    // Обновляем аватарки для пользователей без них
                    await userInfoService.UpdateMissingAvatarsAsync(enrichedFollowers);

                    // Сохраняем в БД
                    await followerDbService.SaveOrUpdateFollowersAsync(enrichedFollowers);

                    logger.LogInformation(
                        "Кеш фоловеров инициализирован из API и сохранен в БД: {Count} фоловеров",
                        enrichedFollowers.Count
                    );
                }
            }
        }
        catch (Exception ex)
        {
            // Логируем ошибку, но не прерываем работу сервиса
            logger.LogError(ex, "Ошибка при инициализации кеша фоловеров");
        }
    }

    /// <summary>
    /// Обновление данных фоловеров из API в фоновом режиме
    /// </summary>
    private async Task UpdateFollowersFromApiAsync()
    {
        try
        {
            var followers = await LoadFollowersFromApiAsync();
            if (followers != null)
            {
                // Обогащаем данные дополнительной информацией
                var enrichedFollowers = await userInfoService.EnrichFollowersInfoAsync(followers);

                // Обновляем аватарки для пользователей без них
                await userInfoService.UpdateMissingAvatarsAsync(enrichedFollowers);

                // Сохраняем в БД (это и есть наш кеш)
                await followerDbService.SaveOrUpdateFollowersAsync(enrichedFollowers);

                logger.LogInformation(
                    "Данные фоловеров обновлены из API: {Count} фоловеров",
                    enrichedFollowers.Count
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении данных фоловеров из API");
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
                // Обогащаем данные дополнительной информацией
                var enrichedFollowers = await userInfoService.EnrichFollowersInfoAsync(followers);

                // Обновляем аватарки для пользователей без них
                await userInfoService.UpdateMissingAvatarsAsync(enrichedFollowers);

                // Сохраняем в БД (это и есть наш кеш)
                await followerDbService.SaveOrUpdateFollowersAsync(enrichedFollowers);

                logger.LogInformation(
                    "Кеш фоловеров обновлен из API и сохранен в БД. Количество: {Count}",
                    enrichedFollowers.Count
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении кеша фоловеров");
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

    private async Task WsClientOnChannelFollow(object? sender, ChannelFollowArgs args)
    {
        try
        {
            // Проверяем, что это событие для нашего канала
            var twEvent = args.Payload.Event;

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

                // Обогащаем данные дополнительной информацией
                var enrichedFollower = await userInfoService.EnrichFollowerInfoAsync(newFollower);

                // Обновляем аватарку если её нет
                await userInfoService.UpdateUserAvatarAsync(enrichedFollower);

                // Сохраняем в БД (это и есть наш кеш)
                var isNew = await followerDbService.SaveOrUpdateFollowerAsync(enrichedFollower);

                if (isNew)
                {
                    logger.LogInformation(
                        "Добавлен новый фоловер: {UserName} (ID: {UserId})",
                        twEvent.UserName,
                        twEvent.UserId
                    );
                }
                else
                {
                    logger.LogInformation(
                        "Обновлен фоловер: {UserName} (ID: {UserId})",
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
    }

    private async Task WsClientOnChannelVipAdd(object? sender, ChannelVipArgs args)
    {
        var twEvent = args.Payload.Event;
        if (twEvent.BroadcasterUserId.Equals(TwitchExstension.ChannelId))
        {
            var newVip = new FollowerInfo()
            {
                FollowedAt = DateTime.UnixEpoch,
                LastUpdated = DateTime.UtcNow,
                UserId = twEvent.UserId,
                UserLogin = twEvent.UserLogin,
                UserName = twEvent.UserName,
                IsModerator = false,
                IsVip = true,
            };

            // Обогащаем данные дополнительной информацией
            var enrichedVip = await userInfoService.EnrichFollowerInfoAsync(newVip);

            // Обновляем аватарку если её нет
            await userInfoService.UpdateUserAvatarAsync(enrichedVip);

            // Сохраняем в БД (это и есть наш кеш)
            await followerDbService.SaveOrUpdateFollowerAsync(enrichedVip);

            logger.LogInformation(
                "Добавлен новый VIP: {UserName} (ID: {UserId})",
                twEvent.UserName,
                twEvent.UserId
            );
        }
    }

    private async Task WsClientOnChannelModeratorAdd(object? sender, ChannelModeratorArgs args)
    {
        var twEvent = args.Payload.Event;
        if (twEvent.BroadcasterUserId.Equals(TwitchExstension.ChannelId))
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

            // Обогащаем данные дополнительной информацией
            var enrichedModerator = await userInfoService.EnrichFollowerInfoAsync(newModerator);

            // Обновляем аватарку если её нет
            await userInfoService.UpdateUserAvatarAsync(enrichedModerator);

            // Сохраняем в БД (это и есть наш кеш)
            await followerDbService.SaveOrUpdateFollowerAsync(enrichedModerator);

            logger.LogInformation(
                "Добавлен новый модератор: {UserName} (ID: {UserId})",
                twEvent.UserName,
                twEvent.UserId
            );
        }
    }

    /// <summary>
    /// Получить всех фоловеров как FollowerInfo
    /// </summary>
    public async Task<List<FollowerInfo>?> GetAllFollowersInfo(bool useCash = false)
    {
        if (tokenService.Token == null || useCash)
        {
            // Если токен недоступен, возвращаем данные из БД (кеша)
            var followersFromDb = await followerDbService.GetAllFollowersFromDbAsync();
            return followersFromDb.Count > 0 ? followersFromDb : null;
        }

        try
        {
            // Пытаемся получить данные из API
            var followers = await LoadFollowersFromApiAsync();
            if (followers != null)
            {
                // Обогащаем данные дополнительной информацией
                var enrichedFollowers = await userInfoService.EnrichFollowersInfoAsync(followers);

                // Обновляем аватарки для пользователей без них
                await userInfoService.UpdateMissingAvatarsAsync(enrichedFollowers);

                // Сохраняем в БД (это и есть наш кеш)
                await followerDbService.SaveOrUpdateFollowersAsync(enrichedFollowers);

                return [.. enrichedFollowers];
            }
        }
        catch (Exception ex)
        {
            // При ошибке API возвращаем данные из БД (кеша)
            var followersFromDb = await followerDbService.GetAllFollowersFromDbAsync();
            if (followersFromDb.Count > 0)
            {
                logger.LogWarning(ex, "API недоступен, используем данные из БД");
                return followersFromDb;
            }

            // Если БД пуста, пробрасываем исключение
            throw new InvalidOperationException(
                $"Ошибка при получении фоловеров канала {ChannelName}",
                ex
            );
        }

        return null;
    }

    /// <summary>
    /// Очистить кеш фоловеров
    /// </summary>
    public async Task ClearFollowersCache()
    {
        await followerDbService.ClearAllFollowersAsync();
        logger.LogInformation("Кеш фоловеров очищен");
    }

    /// <summary>
    /// Получить фоловеров, которые нужно обновить (старше указанного времени)
    /// </summary>
    /// <param name="olderThan">Обновить фоловеров старше этого времени</param>
    public async Task<List<string>> GetFollowersToUpdateAsync(DateTime olderThan)
    {
        return await followerDbService.GetFollowersToUpdateAsync(olderThan);
    }

    /// <summary>
    /// Очистить все данные о фоловерах из базы данных
    /// </summary>
    public async Task<int> ClearAllFollowersFromDbAsync()
    {
        var clearedCount = await followerDbService.ClearAllFollowersAsync();

        logger.LogInformation("Очищено {Count} фоловеров из базы данных", clearedCount);
        return clearedCount;
    }

    /// <summary>
    /// Получить пользователей без аватарок
    /// </summary>
    public async Task<List<FollowerInfo>> GetUsersWithoutAvatarsAsync()
    {
        return await followerDbService.GetUsersWithoutAvatarsAsync();
    }

    /// <summary>
    /// Получить количество пользователей без аватарок
    /// </summary>
    public async Task<int> GetUsersWithoutAvatarsCountAsync()
    {
        return await followerDbService.GetUsersWithoutAvatarsCountAsync();
    }

    /// <summary>
    /// Обновить аватарки для пользователей без них
    /// </summary>
    public async Task<int> UpdateMissingAvatarsAsync()
    {
        try
        {
            // Получаем пользователей без аватарок из БД
            var usersWithoutAvatars = await followerDbService.GetUsersWithoutAvatarsAsync();

            if (usersWithoutAvatars.Count == 0)
            {
                logger.LogInformation("Все пользователи уже имеют аватарки");
                return 0;
            }

            logger.LogInformation(
                "Найдено {Count} пользователей без аватарок, обновляем...",
                usersWithoutAvatars.Count
            );

            // Обновляем аватарки через TwitchUserInfoService
            var updatedCount = await userInfoService.UpdateMissingAvatarsAsync(usersWithoutAvatars);

            if (updatedCount > 0)
            {
                // Получаем только пользователей с обновленными аватарками
                var usersWithUpdatedAvatars = usersWithoutAvatars
                    .Where(u => !string.IsNullOrWhiteSpace(u.ProfileImageUrl))
                    .ToList();

                if (usersWithUpdatedAvatars.Count > 0)
                {
                    // Сохраняем обновленные данные в БД
                    var dbUpdatedCount = await followerDbService.UpdateAvatarsAsync(
                        usersWithUpdatedAvatars
                    );

                    logger.LogInformation(
                        "Успешно обновлено {Count} аватарок в памяти и {DbCount} в БД",
                        updatedCount,
                        dbUpdatedCount
                    );
                }
                else
                {
                    logger.LogWarning(
                        "Аватарки обновились в памяти, но не найдены для сохранения в БД"
                    );
                }
            }

            return updatedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении аватарок пользователей");
            return 0;
        }
    }
}
