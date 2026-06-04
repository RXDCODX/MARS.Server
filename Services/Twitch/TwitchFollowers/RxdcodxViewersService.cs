using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
    FollowerDbService followerDbService
) : BackgroundService, IRxdcodxViewersService
{
    private const string ChannelId = TwitchExstension.ChannelId; // ID канала rxdcodx
    private const string ChannelName = TwitchExstension.Channel;

    private static readonly IEqualityComparer<FollowerInfo> UsersComparer =
        new ValueComparer<FollowerInfo>(
            (e1, e2) =>
                e1 != null
                && e2 != null
                && !string.IsNullOrWhiteSpace(e1.UserId)
                && !string.IsNullOrWhiteSpace(e2.UserId)
                && e1.UserId.Equals(e2.UserId),
            info => int.Parse(info.UserId)
        );

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

                // Актуализируем данные при запуске
                await ActualizeFollowersDataAsync();
            }
            else
            {
                // Если в БД нет данных, загружаем из API
                var followers = await LoadFollowersFromApiAsync();
                if (followers != null)
                {
                    // Обновление TwitchUser выполняется отдельным сервисом
                    // Сохраняем в БД
                    await followerDbService.SaveOrUpdateFollowersAsync(followers);

                    logger.LogInformation(
                        "Кеш фоловеров инициализирован из API и сохранен в БД: {Count} фоловеров",
                        followers.Count
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
    /// Актуализация данных о фоловерах, модераторах и VIP при запуске приложения
    /// </summary>
    private async Task ActualizeFollowersDataAsync()
    {
        try
        {
            logger.LogInformation("Начало актуализации данных о фоловерах, модераторах и VIP");

            // Получаем текущие данные из API
            var currentFollowersFromApi = await LoadFollowersFromApiAsync();

            if (currentFollowersFromApi != null && currentFollowersFromApi.Count > 0)
            {
                // Получаем данные из БД
                var followersFromDb = await followerDbService.GetAllFollowersFromDbAsync();

                // Определяем кого нужно удалить (есть в БД, но нет в API)
                var currentUserIdsFromApi = currentFollowersFromApi
                    .Select(f => f.UserId)
                    .ToHashSet();
                var userIdsToDelete = followersFromDb
                    .Where(f => !currentUserIdsFromApi.Contains(f.UserId))
                    .Select(f => f.UserId)
                    .ToList();

                if (userIdsToDelete.Count > 0)
                {
                    var deletedCount = await followerDbService.DeleteFollowersAsync(
                        userIdsToDelete
                    );
                    logger.LogInformation(
                        "Удалено {Count} пользователей, которые отписались или больше не модераторы/VIP",
                        deletedCount
                    );
                }

                // Обновление TwitchUser выполняется отдельным сервисом
                // Обновляем все данные в БД (статусы могли измениться)
                var savedCount = await followerDbService.SaveOrUpdateFollowersAsync(
                    currentFollowersFromApi
                );

                logger.LogInformation(
                    "Актуализация завершена: обновлено/добавлено {SavedCount} записей, удалено {DeletedCount} записей",
                    savedCount,
                    userIdsToDelete.Count
                );
            }
            else
            {
                logger.LogWarning(
                    "Не удалось получить данные из API для актуализации, используем существующий кеш"
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при актуализации данных фоловеров");
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
                // Обновление TwitchUser выполняется отдельным сервисом
                // Сохраняем в БД (это и есть наш кеш)
                await followerDbService.SaveOrUpdateFollowersAsync(followers);

                logger.LogInformation(
                    "Данные фоловеров обновлены из API: {Count} фоловеров",
                    followers.Count
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

        var pagination = string.Empty;
        var firstRequest = true;
        var list = new HashSet<FollowerInfo>(UsersComparer);

        try
        {
            var result2 = await api.Helix.Moderation.GetModeratorsAsync(
                ChannelId,
                null,
                100,
                null,
                tokenService.Token.AccessToken
            );

            var moderators = result2.Data.Select(mod => new FollowerInfo
            {
                UserId = mod.UserId,
                TwitchUser = TwitchUser.FromModerator(mod),
            });

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

            var vips = result3.Data.Select(vip => new FollowerInfo
            {
                UserId = vip.UserId,
                TwitchUser = TwitchUser.FromVip(vip),
            });

            foreach (FollowerInfo followerInfo in vips)
            {
                list.Add(followerInfo);
            }

            while (!string.IsNullOrWhiteSpace(pagination) || firstRequest)
            {
                firstRequest = false;
                var result = await api.Helix.Channels.GetChannelFollowersAsync(
                    ChannelId,
                    null,
                    100,
                    pagination,
                    tokenService.Token.AccessToken
                );

                pagination = result.Pagination?.Cursor ?? string.Empty;
                var followers = result.Data.Select(follower => new FollowerInfo
                {
                    UserId = follower.UserId,
                });
                foreach (FollowerInfo followerInfo in followers)
                {
                    list.Add(followerInfo);
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
                // Обновление TwitchUser выполняется отдельным сервисом
                // Сохраняем в БД (это и есть наш кеш)
                await followerDbService.SaveOrUpdateFollowersAsync(followers);

                logger.LogInformation(
                    "Кеш фоловеров обновлен из API и сохранен в БД. Количество: {Count}",
                    followers.Count
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
                var newFollower = new FollowerInfo { UserId = twEvent.UserId };

                // Обновление TwitchUser выполняется отдельным сервисом
                // Сохраняем в БД (это и есть наш кеш)
                var isNew = await followerDbService.SaveOrUpdateFollowerAsync(newFollower);

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
            var newVip = new FollowerInfo() { UserId = twEvent.UserId };

            // Обновление TwitchUser выполняется отдельным сервисом
            // Сохраняем в БД (это и есть наш кеш)
            await followerDbService.SaveOrUpdateFollowerAsync(newVip);

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
            var newModerator = new FollowerInfo() { UserId = twEvent.UserId };

            // Обновление TwitchUser выполняется отдельным сервисом
            // Сохраняем в БД (это и есть наш кеш)
            await followerDbService.SaveOrUpdateFollowerAsync(newModerator);

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
                // Обновление TwitchUser выполняется отдельным сервисом
                // Сохраняем в БД (это и есть наш кеш)
                await followerDbService.SaveOrUpdateFollowersAsync(followers);

                return [.. followers];
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
        var result = 0;

        try
        {
            // Получаем пользователей без аватарок из БД
            var usersWithoutAvatars = await followerDbService.GetUsersWithoutAvatarsAsync();

            if (usersWithoutAvatars.Count == 0)
            {
                logger.LogInformation("Все пользователи уже имеют аватарки");
            }
            else
            {
                // Обновление TwitchUser (и аватарок) выполняется отдельным сервисом
                logger.LogInformation(
                    "Найдено {Count} пользователей без аватарок (обновление выполняется отдельным сервисом)",
                    usersWithoutAvatars.Count
                );
                result = 0;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении аватарок пользователей");
        }

        return result;
    }

    /// <summary>
    /// Актуализировать данные о фоловерах, модераторах и VIP (публичный метод)
    /// </summary>
    public async Task ActualizeFollowersAsync()
    {
        await ActualizeFollowersDataAsync();
    }
}
