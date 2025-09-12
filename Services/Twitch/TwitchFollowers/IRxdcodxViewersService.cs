using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;

namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Интерфейс для сервиса получения информации о зрителях канала rxdcodx
/// </summary>
public interface IRxdcodxViewersService
{
    /// <summary>
    /// Получить всех фоловеров как FollowerInfo
    /// </summary>
    Task<List<FollowerInfo>?> GetAllFollowersInfo(bool useCash = false);

    /// <summary>
    /// Принудительно обновить кеш фоловеров
    /// </summary>
    Task RefreshFollowersCacheAsync();

    /// <summary>
    /// Очистить кеш фоловеров
    /// </summary>
    Task ClearFollowersCache();

    /// <summary>
    /// Получить фоловеров, которые нужно обновить (старше указанного времени)
    /// </summary>
    /// <param name="olderThan">Обновить фоловеров старше этого времени</param>
    Task<List<string>> GetFollowersToUpdateAsync(DateTime olderThan);

    /// <summary>
    /// Очистить все данные о фоловерах из базы данных
    /// </summary>
    Task<int> ClearAllFollowersFromDbAsync();
}
