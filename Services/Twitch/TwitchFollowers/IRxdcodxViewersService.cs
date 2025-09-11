using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelVIPs;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;

namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Интерфейс для сервиса получения информации о зрителях канала rxdcodx
/// </summary>
public interface IRxdcodxViewersService
{
    Task<ChannelUsersResult?> GetChannelUsersAsync();

    /// <summary>
    /// Получить всех фоловеров канала rxdcodx (только при наличии токена)
    /// Для получения данных из кеша используйте GetAllFollowersInfo()
    /// </summary>
    Task<List<ChannelFollower>?> GetAllFollowers();

    /// <summary>
    /// Получить всех VIP канала rxdcodx
    /// </summary>
    Task<List<ChannelVIPsResponseModel>?> GetAllViPs();

    /// <summary>
    /// Получить всех модераторов канала rxdcodx
    /// </summary>
    Task<List<Moderator>?> GetModerators();

    /// <summary>
    /// Получить количество фоловеров канала rxdcodx
    /// </summary>
    Task<int> GetFollowersCount();

    /// <summary>
    /// Получить количество VIP канала rxdcodx
    /// </summary>
    Task<int> GetViPsCount();

    /// <summary>
    /// Получить количество модераторов канала rxdcodx
    /// </summary>
    Task<int> GetModeratorsCount();

    /// <summary>
    /// Проверить, является ли пользователь фоловером канала rxdcodx
    /// </summary>
    /// <param name="userId">ID пользователя для проверки</param>
    Task<bool> IsUserFollower(string userId);

    /// <summary>
    /// Проверить, является ли пользователь VIP канала rxdcodx
    /// </summary>
    /// <param name="userId">ID пользователя для проверки</param>
    Task<bool> IsUserVip(string userId);

    /// <summary>
    /// Проверить, является ли пользователь модератором канала rxdcodx
    /// </summary>
    /// <param name="userId">ID пользователя для проверки</param>
    Task<bool> IsUserModerator(string userId);

    /// <summary>
    /// Принудительно обновить кеш фоловеров
    /// </summary>
    Task RefreshFollowersCacheAsync();

    /// <summary>
    /// Получить всех фоловеров как FollowerInfo
    /// </summary>
    Task<List<FollowerInfo>?> GetAllFollowersInfo();

    /// <summary>
    /// Получить информацию о конкретном фоловере
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    Task<FollowerInfo?> GetFollowerInfo(string userId);

    /// <summary>
    /// Получить количество фоловеров в кеше
    /// </summary>
    int GetCachedFollowersCount();

    /// <summary>
    /// Очистить кеш фоловеров
    /// </summary>
    void ClearFollowersCache();
}
