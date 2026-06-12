using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Interfaces;
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
                    .Where(f =>
                        f.TwitchUser == null
                        || string.IsNullOrWhiteSpace(f.TwitchUser.ProfileImageUrl)
                    )
                    .Select(f => f.UserId),
            ];
    }
}
