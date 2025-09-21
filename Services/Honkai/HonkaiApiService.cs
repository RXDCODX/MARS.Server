using MarchSeven;
using MarchSeven.Models.Core;
using MarchSeven.Models.Core.Cookie;
using MarchSeven.Models.HonkaiStarRail.Entitys;
using MarchSeven.Models.HonkaiStarRail.StarRailDailyNote;
using MarchSeven.Models.HoYoLab;
using MarchSeven.Util.Errors;
using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai;

public interface IHonkaiApiService
{
    Task<StarRailUser?> GetStarRailUserAsync(DailyAutoMarkupUser user, HttpClient httpClient);
    Task<(bool Success, string? RewardName, int? Amount)> ClaimDailyRewardAsync(
        DailyAutoMarkupUser user,
        HttpClient httpClient
    );
    Task<StarRailDailyNote?> GetDailyNoteAsync(DailyAutoMarkupUser user, HttpClient httpClient);
    Task<UserStatsData?> GetUserStatsAsync(DailyAutoMarkupUser user, HttpClient httpClient);
}

public class HonkaiApiService(ILogger<HonkaiApiService> logger, IHostEnvironment environment)
    : IHonkaiApiService
{
    public async Task<StarRailUser?> GetStarRailUserAsync(
        DailyAutoMarkupUser user,
        HttpClient httpClient
    )
    {
        StarRailUser? result = null;
        
        if (user != null && httpClient != null)
        {
            try
            {
                var client = CreateMarchSevenClient(user, httpClient);
                var gameRoles = await client.GetGameRoles();

                var starRailRole = gameRoles.Data?.List?.FirstOrDefault(r =>
                    r.GameRegionName == "hkrpg_global"
                );

                if (starRailRole != null)
                {
                    var hsrUser = new StarRailUser(int.Parse(starRailRole.GameUid));
                    logger.LogDebug("UID: {Uid}, Server: {Server}", hsrUser.Uid, hsrUser.Server);
                    result = hsrUser;
                }
                else
                {
                    logger.LogDebug("Star Rail role not found for user {UserId}", user.Id);
                }
            }
            catch (Exception ex)
            {
                if (!environment.IsProduction())
                {
                    logger.LogError(ex, "Error getting Star Rail user for user {UserId}", user.Id);
                }
            }
        }
        
        return result;
    }

    public async Task<(bool Success, string? RewardName, int? Amount)> ClaimDailyRewardAsync(
        DailyAutoMarkupUser user,
        HttpClient httpClient
    )
    {
        (bool Success, string? RewardName, int? Amount) result = (false, null, null);
        
        if (user != null && httpClient != null)
        {
            try
            {
                var client = CreateMarchSevenClient(user, httpClient);
                var response = await client.StarRail.ClaimDailyRewardAsync();

                logger.LogInformation(
                    "Daily reward claimed successfully for user {UserId}: {RewardName} x{Amount}",
                    user.Id,
                    response.RewardName,
                    response.Amount
                );

                result = (true, response.RewardName, response.Amount);
            }
            catch (DailyRewardAlreadyReceivedException)
            {
                logger.LogInformation("Daily reward already received for user {UserId}", user.Id);
                result = (false, null, null);
            }
            catch (Exception ex)
            {
                if (!environment.IsProduction())
                {
                    logger.LogError(ex, "Error claiming daily reward for user {UserId}", user.Id);
                }
                throw;
            }
        }
        
        return result;
    }

    public async Task<StarRailDailyNote?> GetDailyNoteAsync(
        DailyAutoMarkupUser user,
        HttpClient httpClient
    )
    {
        StarRailDailyNote? result = null;
        
        if (user != null && httpClient != null)
        {
            try
            {
                var starRailUser = await GetStarRailUserAsync(user, httpClient);
                if (starRailUser != null)
                {
                    var client = CreateMarchSevenClient(user, httpClient);
                    result = await client.StarRail.FetchDailyNoteAsync(starRailUser);
                }
            }
            catch (Exception ex)
            {
                if (!environment.IsProduction())
                {
                    logger.LogError(ex, "Error getting daily note for user {UserId}", user.Id);
                }
            }
        }
        
        return result;
    }

    public async Task<UserStatsData?> GetUserStatsAsync(
        DailyAutoMarkupUser user,
        HttpClient httpClient
    )
    {
        UserStatsData? result = null;
        
        if (user != null && httpClient != null)
        {
            try
            {
                var client = CreateMarchSevenClient(user, httpClient);
                var accountInfo = await client.StarRail.FetchUserStatsAsync();

                if (accountInfo?.Data?.GameLists != null)
                {
                    result = accountInfo.Data;
                }
                else
                {
                    logger.LogWarning("Failed to get account info for user {UserId}", user.Id);
                }
            }
            catch (Exception ex)
            {
                if (!environment.IsProduction())
                {
                    logger.LogError(ex, "Error getting user stats for user {UserId}", user.Id);
                }
            }
        }
        
        return result;
    }

    private static MarchSevenClient CreateMarchSevenClient(
        DailyAutoMarkupUser user,
        HttpClient httpClient
    )
    {
        var cookieV2 = new CookieV2
        {
            LTokenV2 = user.LTokenV2,
            LtMidV2 = user.LtmidV2,
            LtUidV2 = user.LtuidV2,
        };

        var clientData = new ClientData { HttpClient = httpClient, Language = "ru-ru" };

        return MarchSevenClient.Create(cookieV2, clientData);
    }
}
