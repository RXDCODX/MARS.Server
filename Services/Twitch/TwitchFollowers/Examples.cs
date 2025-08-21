using MARS.Server.Services.Twitch.TwitchFollowers;

namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Примеры использования сервиса RxdcodxViewersService
/// </summary>
public static class Examples
{
    /// <summary>
    /// Пример получения статистики канала
    /// </summary>
    public static async Task<string> GetChannelStatsExample(IRxdcodxViewersService viewersService)
    {
        try
        {
            var followersCount = await viewersService.GetFollowersCount();
            var vipsCount = await viewersService.GetVIPsCount();
            var moderatorsCount = await viewersService.GetModeratorsCount();

            return $"📊 Статистика канала rxdcodx:\n" +
                   $"👥 Фоловеры: {followersCount}\n" +
                   $"⭐ VIP: {vipsCount}\n" +
                   $"🛡️ Модераторы: {moderatorsCount}";
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Пример проверки статуса пользователя
    /// </summary>
    public static async Task<string> CheckUserStatusExample(IRxdcodxViewersService viewersService, string userId, string username)
    {
        try
        {
            var isFollower = await viewersService.IsUserFollower(userId);
            var isVIP = await viewersService.IsUserVIP(userId);
            var isModerator = await viewersService.IsUserModerator(userId);

            if (isModerator)
                return $"🛡️ {username} - модератор канала rxdcodx";
            else if (isVIP)
                return $"⭐ {username} - VIP канала rxdcodx";
            else if (isFollower)
                return $"👥 {username} - фоловер канала rxdcodx";
            else
                return $"👤 {username} - обычный зритель канала rxdcodx";
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Пример получения списка фоловеров с ограничением
    /// </summary>
    public static async Task<string> GetFollowersWithLimitExample(IRxdcodxViewersService viewersService, int limit = 10)
    {
        try
        {
            var followers = await viewersService.GetAllFollowers();
            if (followers == null)
                return "❌ Токен недоступен";

            var limitedFollowers = followers.Take(limit).ToList();
            var result = $"👥 Последние {limitedFollowers.Count} фоловеров канала rxdcodx:\n";
            
            foreach (var follower in limitedFollowers)
            {
                result += $"• {follower.UserName} (@{follower.UserLogin})\n";
            }

            if (followers.Count > limit)
                result += $"\n... и еще {followers.Count - limit} фоловеров";

            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Пример получения списка VIP с ограничением
    /// </summary>
    public static async Task<string> GetVIPsWithLimitExample(IRxdcodxViewersService viewersService, int limit = 10)
    {
        try
        {
            var vips = await viewersService.GetAllViPs();
            if (vips == null)
                return "❌ Токен недоступен";

            var limitedVIPs = vips.Take(limit).ToList();
            var result = $"⭐ VIP канала rxdcodx ({limitedVIPs.Count}):\n";
            
            foreach (var vip in limitedVIPs)
            {
                result += $"• {vip.UserName} (@{vip.UserLogin})\n";
            }

            if (vips.Count > limit)
                result += $"\n... и еще {vips.Count - limit} VIP";

            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Пример получения списка модераторов
    /// </summary>
    public static async Task<string> GetModeratorsExample(IRxdcodxViewersService viewersService)
    {
        try
        {
            var moderators = await viewersService.GetModerators();
            if (moderators == null)
                return "❌ Токен недоступен";

            if (moderators.Count == 0)
                return "🛡️ На канале rxdcodx нет модераторов";

            var result = $"🛡️ Модераторы канала rxdcodx ({moderators.Count}):\n";
            
            foreach (var moderator in moderators)
            {
                result += $"• {moderator.UserName} (@{moderator.UserLogin})\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Пример комплексной проверки пользователя
    /// </summary>
    public static async Task<string> ComprehensiveUserCheckExample(IRxdcodxViewersService viewersService, string userId, string username)
    {
        try
        {
            var isFollower = await viewersService.IsUserFollower(userId);
            var isVIP = await viewersService.IsUserVIP(userId);
            var isModerator = await viewersService.IsUserModerator(userId);

            var roles = new List<string>();
            if (isModerator) roles.Add("🛡️ Модератор");
            if (isVIP) roles.Add("⭐ VIP");
            if (isFollower) roles.Add("👥 Фоловер");

            if (roles.Count == 0)
                return $"👤 {username} - обычный зритель канала rxdcodx";

            var rolesText = string.Join(" | ", roles);
            return $"👤 {username} - {rolesText} канала rxdcodx";
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка: {ex.Message}";
        }
    }
}
