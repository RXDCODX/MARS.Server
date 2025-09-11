using MARS.Server.Services.Twitch.TwitchFollowers;
using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;
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
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var allUsers = await viewersService.GetAllFollowersInfo();
            return allUsers == null ? Unauthorized("Токен недоступен") : Ok(allUsers);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении всех пользователей");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить всех фоловеров канала rxdcodx
    /// </summary>
    [HttpGet("followers")]
    public async Task<IActionResult> GetFollowers()
    {
        try
        {
            var allUsers = await viewersService.GetAllFollowersInfo();
            if (allUsers == null)
            {
                return Unauthorized("Токен недоступен");
            }

            var followers = allUsers.Where(u => u is { IsModerator: false, IsVip: false }).ToList();
            return Ok(followers);
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
            var allUsers = await viewersService.GetAllFollowersInfo();
            if (allUsers == null)
            {
                return Unauthorized("Токен недоступен");
            }

            var vips = allUsers.Where(u => u.IsVip).ToList();
            return Ok(vips);
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
            var allUsers = await viewersService.GetAllFollowersInfo();
            if (allUsers == null)
            {
                return Unauthorized("Токен недоступен");
            }

            var moderators = allUsers.Where(u => u.IsModerator).ToList();
            return Ok(moderators);
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
            var allUsers = await viewersService.GetAllFollowersInfo();
            if (allUsers == null)
            {
                return Unauthorized("Токен недоступен");
            }

            var followersCount = allUsers.Count(u => u is { IsModerator: false, IsVip: false });
            var vipsCount = allUsers.Count(u => u.IsVip);
            var moderatorsCount = allUsers.Count(u => u.IsModerator);

            return Ok(
                new
                {
                    FollowersCount = followersCount,
                    VIPsCount = vipsCount,
                    ModeratorsCount = moderatorsCount,
                    TotalSpecialUsers = vipsCount + moderatorsCount,
                    TotalUsers = allUsers.Count,
                    CachedUsersCount = viewersService.GetCachedFollowersCount(),
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
            var userInfo = await viewersService.GetFollowerInfo(userId);
            if (userInfo == null)
            {
                return Ok(
                    new
                    {
                        UserId = userId,
                        IsFollower = false,
                        IsVIP = false,
                        IsModerator = false,
                        Status = "Viewer",
                        Message = "Пользователь не найден в кеше",
                    }
                );
            }

            return Ok(
                new
                {
                    UserId = userId,
                    userInfo.UserName,
                    userInfo.UserLogin,
                    userInfo.FollowedAt,
                    userInfo.LastUpdated,
                    IsFollower = userInfo is { IsModerator: false, IsVip: false },
                    IsVIP = userInfo.IsVip,
                    userInfo.IsModerator,
                    Status = userInfo.IsModerator ? "Moderator"
                    : userInfo.IsVip ? "VIP"
                    : "Follower",
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при проверке статуса пользователя {UserId}", userId);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Принудительно обновить кеш фоловеров
    /// </summary>
    [HttpPost("refresh-cache")]
    public async Task<IActionResult> RefreshFollowersCache()
    {
        try
        {
            await viewersService.RefreshFollowersCacheAsync();
            return Ok(new { Message = "Кеш фоловеров успешно обновлен" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении кеша фоловеров");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить всех пользователей как FollowerInfo
    /// </summary>
    [HttpGet("followers-info")]
    public async Task<IActionResult> GetFollowersInfo()
    {
        try
        {
            var followersInfo = await viewersService.GetAllFollowersInfo();
            return followersInfo == null ? Unauthorized("Токен недоступен") : Ok(followersInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении информации о фоловерах");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить информацию о конкретном пользователе
    /// </summary>
    [HttpGet("user/{userId}/info")]
    public async Task<IActionResult> GetUserInfo(string userId)
    {
        try
        {
            var userInfo = await viewersService.GetFollowerInfo(userId);
            return userInfo == null ? NotFound($"Пользователь {userId} не найден") : Ok(userInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении информации о пользователе {UserId}", userId);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Очистить кеш пользователей
    /// </summary>
    [HttpPost("clear-cache")]
    public IActionResult ClearCache()
    {
        try
        {
            viewersService.ClearFollowersCache();
            return Ok(new { Message = "Кеш пользователей успешно очищен" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при очистке кеша пользователей");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
