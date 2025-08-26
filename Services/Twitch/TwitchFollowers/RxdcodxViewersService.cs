using MARS.Server.Services.Twitch.Management;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelVIPs;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;

namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Сервис для получения информации о зрителях канала rxdcodx
/// </summary>
public class RxdcodxViewersService(ITwitchAPI api, TokenService tokenService)
    : IRxdcodxViewersService
{
    private const string ChannelId = "785975641"; // ID канала rxdcodx
    private const string ChannelName = "rxdcodx";

    /// <summary>
    /// Получить всех фоловеров канала rxdcodx
    /// </summary>
    /// <returns>Список фоловеров или null если токен недоступен</returns>
    public async Task<List<ChannelFollower>?> GetAllFollowers()
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
            // Логирование ошибки можно добавить здесь
            throw new InvalidOperationException(
                $"Ошибка при получении фоловеров канала {ChannelName}",
                ex
            );
        }
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
        return followers?.Count ?? 0;
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
            return false;
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
            return false;
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
}
