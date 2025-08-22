using MARS.Server.Services.Twitch.TwitchFollowers;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для работы с информацией о зрителях канала rxdcodx
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RxdcodxViewersController(
    IRxdcodxViewersService viewersService,
    ILogger<RxdcodxViewersController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить всех фоловеров канала rxdcodx
    /// </summary>
    [HttpGet("followers")]
    public async Task<IActionResult> GetFollowers()
    {
        try
        {
            var followers = await viewersService.GetAllFollowers();
            return followers == null
                ? Unauthorized("Токен недоступен")
                : Ok(
                    new
                    {
                        followers.Count,
                        Followers = followers.Select(f => new
                        {
                            f.UserId,
                            f.UserLogin,
                            f.UserName,
                            f.FollowedAt,
                        }),
                    }
                );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении фоловеров");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить всех VIP канала rxdcodx
    /// </summary>
    [HttpGet("vips")]
    public async Task<IActionResult> GetViPs()
    {
        try
        {
            var vips = await viewersService.GetAllViPs();
            return vips == null
                ? Unauthorized("Токен недоступен")
                : Ok(
                    new
                    {
                        vips.Count,
                        VIPs = vips.Select(v => new
                        {
                            v.UserId,
                            v.UserLogin,
                            v.UserName,
                        }),
                    }
                );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении VIP");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить всех модераторов канала rxdcodx
    /// </summary>
    [HttpGet("moderators")]
    public async Task<IActionResult> GetModerators()
    {
        try
        {
            var moderators = await viewersService.GetModerators();
            return moderators == null
                ? Unauthorized("Токен недоступен")
                : Ok(
                    new
                    {
                        moderators.Count,
                        Moderators = moderators.Select(m => new
                        {
                            m.UserId,
                            m.UserLogin,
                            m.UserName,
                        }),
                    }
                );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении модераторов");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить статистику канала rxdcodx
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var followersCount = await viewersService.GetFollowersCount();
            var vipsCount = await viewersService.GetViPsCount();
            var moderatorsCount = await viewersService.GetModeratorsCount();

            return Ok(
                new
                {
                    FollowersCount = followersCount,
                    VIPsCount = vipsCount,
                    ModeratorsCount = moderatorsCount,
                    TotalSpecialUsers = vipsCount + moderatorsCount,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статистики");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Проверить статус пользователя
    /// </summary>
    [HttpGet("user/{userId}/status")]
    public async Task<IActionResult> GetUserStatus(string userId)
    {
        try
        {
            var isFollower = await viewersService.IsUserFollower(userId);
            var isVip = await viewersService.IsUserVip(userId);
            var isModerator = await viewersService.IsUserModerator(userId);

            return Ok(
                new
                {
                    UserId = userId,
                    IsFollower = isFollower,
                    IsVIP = isVip,
                    IsModerator = isModerator,
                    Status = isModerator ? "Moderator"
                    : isVip ? "VIP"
                    : isFollower ? "Follower"
                    : "Viewer",
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при проверке статуса пользователя {UserId}", userId);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
