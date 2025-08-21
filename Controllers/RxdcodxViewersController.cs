using Microsoft.AspNetCore.Mvc;
using MARS.Server.Services.Twitch.TwitchFollowers;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для работы с информацией о зрителях канала rxdcodx
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RxdcodxViewersController : ControllerBase
{
    private readonly IRxdcodxViewersService _viewersService;
    private readonly ILogger<RxdcodxViewersController> _logger;

    public RxdcodxViewersController(
        IRxdcodxViewersService viewersService,
        ILogger<RxdcodxViewersController> logger)
    {
        _viewersService = viewersService;
        _logger = logger;
    }

    /// <summary>
    /// Получить всех фоловеров канала rxdcodx
    /// </summary>
    [HttpGet("followers")]
    public async Task<IActionResult> GetFollowers()
    {
        try
        {
            var followers = await _viewersService.GetAllFollowers();
            if (followers == null)
            {
                return Unauthorized("Токен недоступен");
            }

            return Ok(new
            {
                Count = followers.Count,
                Followers = followers.Select(f => new
                {
                    f.UserId,
                    f.UserLogin,
                    f.UserName,
                    f.FollowedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении фоловеров");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить всех VIP канала rxdcodx
    /// </summary>
    [HttpGet("vips")]
    public async Task<IActionResult> GetVIPs()
    {
        try
        {
            var vips = await _viewersService.GetAllViPs();
            if (vips == null)
            {
                return Unauthorized("Токен недоступен");
            }

            return Ok(new
            {
                Count = vips.Count,
                VIPs = vips.Select(v => new
                {
                    v.UserId,
                    v.UserLogin,
                    v.UserName
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении VIP");
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
            var moderators = await _viewersService.GetModerators();
            if (moderators == null)
            {
                return Unauthorized("Токен недоступен");
            }

            return Ok(new
            {
                Count = moderators.Count,
                Moderators = moderators.Select(m => new
                {
                    m.UserId,
                    m.UserLogin,
                    m.UserName
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении модераторов");
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
            var followersCount = await _viewersService.GetFollowersCount();
            var vipsCount = await _viewersService.GetVIPsCount();
            var moderatorsCount = await _viewersService.GetModeratorsCount();

            return Ok(new
            {
                FollowersCount = followersCount,
                VIPsCount = vipsCount,
                ModeratorsCount = moderatorsCount,
                TotalSpecialUsers = vipsCount + moderatorsCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статистики");
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
            var isFollower = await _viewersService.IsUserFollower(userId);
            var isVIP = await _viewersService.IsUserVIP(userId);
            var isModerator = await _viewersService.IsUserModerator(userId);

            return Ok(new
            {
                UserId = userId,
                IsFollower = isFollower,
                IsVIP = isVIP,
                IsModerator = isModerator,
                Status = isModerator ? "Moderator" : isVIP ? "VIP" : isFollower ? "Follower" : "Viewer"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке статуса пользователя {UserId}", userId);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
