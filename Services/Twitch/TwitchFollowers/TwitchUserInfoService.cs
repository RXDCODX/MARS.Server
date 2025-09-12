using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;
using User = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Сервис для получения дополнительной информации о пользователях из Twitch API
/// </summary>
public class TwitchUserInfoService(
    ITwitchAPI api,
    TokenService tokenService,
    ILogger<TwitchUserInfoService> logger
)
{
    /// <summary>
    /// Получить дополнительную информацию о пользователе
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Информация о пользователе или null</returns>
    public async Task<User?> GetUserInfoAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || tokenService.Token?.AccessToken == null)
        {
            return null;
        }

        try
        {
            var response = await api.Helix.Users.GetUsersAsync(
                ids: [userId],
                accessToken: tokenService.Token.AccessToken
            );

            return response.Users.FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении информации о пользователе {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Получить дополнительную информацию о нескольких пользователях
    /// </summary>
    /// <param name="userIds">Список ID пользователей</param>
    /// <returns>Словарь с информацией о пользователях</returns>
    public async Task<Dictionary<string, User>> GetUsersInfoAsync(ICollection<string> userIds)
    {
        var result = new Dictionary<string, User>();

        if (userIds.Count == 0 || tokenService.Token?.AccessToken == null)
        {
            return result;
        }

        var userIdsList = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();

        if (userIdsList.Count == 0)
        {
            return result;
        }

        try
        {
            // Twitch API позволяет получать до 100 пользователей за запрос
            const int batchSize = 100;

            logger.LogDebug(
                "Запрашиваем информацию о {Count} пользователях из Twitch API",
                userIdsList.Count
            );

            for (var i = 0; i < userIdsList.Count; i += batchSize)
            {
                var batch = userIdsList.Skip(i).Take(batchSize).ToList();

                logger.LogDebug(
                    "Запрашиваем batch {BatchStart}-{BatchEnd} из {Total}",
                    i,
                    i + batch.Count,
                    userIdsList.Count
                );

                var response = await api.Helix.Users.GetUsersAsync(
                    ids: [.. batch],
                    accessToken: tokenService.Token.AccessToken
                );

                logger.LogDebug(
                    "Получен ответ от Twitch API: {Count} пользователей",
                    response.Users.Length
                );

                foreach (var user in response.Users)
                {
                    result[user.Id] = user;
                    logger.LogDebug(
                        "Добавлен пользователь {UserId}: {DisplayName}, Avatar: {Avatar}",
                        user.Id,
                        user.DisplayName,
                        user.ProfileImageUrl ?? "null"
                    );
                }
            }

            logger.LogDebug("Итого получено {Count} пользователей с информацией", result.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении информации о пользователях");
        }

        return result;
    }

    /// <summary>
    /// Получить цвет чата пользователя
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Цвет чата или null</returns>
    public async Task<string?> GetUserChatColorAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || tokenService.Token?.AccessToken == null)
        {
            return null;
        }

        try
        {
            var response = await api.Helix.Chat.GetUserChatColorAsync(
                userIds: [userId],
                accessToken: tokenService.Token.AccessToken
            );

            return response.Data.FirstOrDefault()?.Color;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении цвета чата пользователя {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Получить цвета чата для нескольких пользователей
    /// </summary>
    /// <param name="userIds">Список ID пользователей</param>
    /// <returns>Словарь с цветами чата</returns>
    public async Task<Dictionary<string, string?>> GetUsersChatColorsAsync(
        IEnumerable<string> userIds
    )
    {
        var result = new Dictionary<string, string?>();

        IEnumerable<string> enumerable = userIds as string[] ?? [.. userIds];
        if (!enumerable.Any() || tokenService.Token?.AccessToken == null)
        {
            return result;
        }

        var userIdsList = enumerable.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();

        if (userIdsList.Count == 0)
        {
            return result;
        }

        try
        {
            // Twitch API позволяет получать до 100 пользователей за запрос
            const int batchSize = 100;

            for (var i = 0; i < userIdsList.Count; i += batchSize)
            {
                var batch = userIdsList.Skip(i).Take(batchSize);

                var response = await api.Helix.Chat.GetUserChatColorAsync(
                    userIds: [.. batch],
                    accessToken: tokenService.Token.AccessToken
                );

                foreach (var userColor in response.Data)
                {
                    result[userColor.UserId] = userColor.Color;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении цветов чата пользователей");
        }

        return result;
    }

    /// <summary>
    /// Обогатить FollowerInfo дополнительной информацией из Twitch API
    /// </summary>
    /// <param name="followerInfo">Базовая информация о фоловере</param>
    /// <returns>Обновленная информация о фоловере</returns>
    public async Task<FollowerInfo> EnrichFollowerInfoAsync(FollowerInfo followerInfo)
    {
        if (string.IsNullOrWhiteSpace(followerInfo.UserId))
        {
            return followerInfo;
        }

        try
        {
            // Получаем информацию о пользователе и цвете чата параллельно
            var userInfoTask = GetUserInfoAsync(followerInfo.UserId);
            var chatColorTask = GetUserChatColorAsync(followerInfo.UserId);

            await Task.WhenAll(userInfoTask, chatColorTask);

            var userInfo = await userInfoTask;
            var chatColor = await chatColorTask;

            // Обновляем информацию
            if (userInfo != null)
            {
                followerInfo.DisplayName = userInfo.DisplayName;
                followerInfo.ProfileImageUrl = userInfo.ProfileImageUrl;
                followerInfo.UserName = userInfo.Login; // Обновляем UserName на случай изменений
            }

            if (!string.IsNullOrWhiteSpace(chatColor))
            {
                followerInfo.ChatColor = chatColor;
            }

            followerInfo.LastUpdated = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обогащении информации о фоловере {UserId}",
                followerInfo.UserId
            );
        }

        return followerInfo;
    }

    /// <summary>
    /// Обогатить список FollowerInfo дополнительной информацией из Twitch API
    /// </summary>
    /// <param name="followersInfo">Список информации о фоловерах</param>
    /// <returns>Обновленный список информации о фоловерах</returns>
    public async Task<ICollection<FollowerInfo>> EnrichFollowersInfoAsync(
        ICollection<FollowerInfo> followersInfo
    )
    {
        if (followersInfo.Count == 0)
        {
            return followersInfo;
        }

        try
        {
            var userIds = followersInfo.Select(f => f.UserId).ToList();

            // Получаем информацию о пользователях и цветах чата параллельно
            var usersInfoTask = GetUsersInfoAsync(userIds);
            var chatColorsTask = GetUsersChatColorsAsync(userIds);

            await Task.WhenAll(usersInfoTask, chatColorsTask);

            var usersInfo = await usersInfoTask;
            var chatColors = await chatColorsTask;

            // Обновляем информацию для каждого фоловера
            foreach (var followerInfo in followersInfo)
            {
                if (usersInfo.TryGetValue(followerInfo.UserId, out var userInfo))
                {
                    followerInfo.DisplayName = userInfo.DisplayName;
                    followerInfo.ProfileImageUrl = userInfo.ProfileImageUrl;
                    followerInfo.UserName = userInfo.Login;
                }

                if (
                    chatColors.TryGetValue(followerInfo.UserId, out var chatColor)
                    && !string.IsNullOrWhiteSpace(chatColor)
                )
                {
                    followerInfo.ChatColor = chatColor;
                }

                followerInfo.LastUpdated = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при массовом обогащении информации о фоловерах");
        }

        return followersInfo;
    }

    /// <summary>
    /// Получить список пользователей без аватарок
    /// </summary>
    /// <param name="followersInfo">Список информации о фоловерах</param>
    /// <returns>Список UserId пользователей без аватарок</returns>
    public List<string> GetUsersWithoutAvatars(ICollection<FollowerInfo> followersInfo)
    {
        return followersInfo.Count == 0
            ? []
            :
            [
                .. followersInfo
                    .Where(f => string.IsNullOrWhiteSpace(f.ProfileImageUrl))
                    .Select(f => f.UserId),
            ];
    }

    /// <summary>
    /// Обновить аватарки для пользователей без них
    /// </summary>
    /// <param name="followersInfo">Список информации о фоловерах</param>
    /// <returns>Количество обновленных аватарок</returns>
    public async Task<int> UpdateMissingAvatarsAsync(ICollection<FollowerInfo> followersInfo)
    {
        if (followersInfo.Count == 0)
        {
            return 0;
        }

        var usersWithoutAvatars = GetUsersWithoutAvatars(followersInfo);

        if (usersWithoutAvatars.Count == 0)
        {
            logger.LogInformation("Все пользователи уже имеют аватарки");
            return 0;
        }

        logger.LogInformation(
            "Найдено {Count} пользователей без аватарок",
            usersWithoutAvatars.Count
        );

        try
        {
            var usersInfo = await GetUsersInfoAsync(usersWithoutAvatars);
            logger.LogDebug(
                "Получена информация о {Count} пользователях из Twitch API",
                usersInfo.Count
            );

            var updatedCount = 0;

            foreach (var followerInfo in followersInfo)
            {
                if (
                    string.IsNullOrWhiteSpace(followerInfo.ProfileImageUrl)
                    && usersInfo.TryGetValue(followerInfo.UserId, out var userInfo)
                )
                {
                    if (!string.IsNullOrWhiteSpace(userInfo.ProfileImageUrl))
                    {
                        var oldAvatar = followerInfo.ProfileImageUrl;
                        followerInfo.ProfileImageUrl = userInfo.ProfileImageUrl;
                        followerInfo.LastUpdated = DateTime.UtcNow;
                        updatedCount++;

                        logger.LogInformation(
                            "Обновлена аватарка для пользователя {UserName} (ID: {UserId}): {OldAvatar} -> {NewAvatar}",
                            followerInfo.UserName,
                            followerInfo.UserId,
                            oldAvatar ?? "null",
                            followerInfo.ProfileImageUrl
                        );
                    }
                    else
                    {
                        logger.LogDebug(
                            "Пользователь {UserId} не имеет аватарки в Twitch API",
                            followerInfo.UserId
                        );
                    }
                }
                else if (string.IsNullOrWhiteSpace(followerInfo.ProfileImageUrl))
                {
                    logger.LogDebug(
                        "Не найдена информация о пользователе {UserId} в Twitch API",
                        followerInfo.UserId
                    );
                }
            }

            logger.LogInformation("Обновлено {Count} аватарок пользователей", updatedCount);
            return updatedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении аватарок пользователей");
            return 0;
        }
    }

    /// <summary>
    /// Обновить аватарки для конкретного пользователя
    /// </summary>
    /// <param name="followerInfo">Информация о фоловере</param>
    /// <returns>True если аватарка была обновлена</returns>
    public async Task<bool> UpdateUserAvatarAsync(FollowerInfo followerInfo)
    {
        if (string.IsNullOrWhiteSpace(followerInfo.UserId))
        {
            return false;
        }

        try
        {
            var userInfo = await GetUserInfoAsync(followerInfo.UserId);

            if (userInfo != null && !string.IsNullOrWhiteSpace(userInfo.ProfileImageUrl))
            {
                followerInfo.ProfileImageUrl = userInfo.ProfileImageUrl;
                followerInfo.LastUpdated = DateTime.UtcNow;

                logger.LogDebug(
                    "Обновлена аватарка для пользователя {UserName} (ID: {UserId})",
                    followerInfo.UserName,
                    followerInfo.UserId
                );
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обновлении аватарки для пользователя {UserId}",
                followerInfo.UserId
            );
            return false;
        }
    }
}
