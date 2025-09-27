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
    public async Task<ActionResult<List<FollowerInfo>>> GetAll([FromQuery] bool forceUseCash)
    {
        try
        {
            var allUsers = await viewersService.GetAllFollowersInfo(forceUseCash);
            return allUsers == null ? Unauthorized("Токен недоступен") : Ok(allUsers);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении всех пользователей");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить пользователей без аватарок
    /// </summary>
    [HttpGet("without-avatars")]
    public async Task<ActionResult<List<FollowerInfo>>> GetUsersWithoutAvatars()
    {
        try
        {
            var usersWithoutAvatars = await viewersService.GetUsersWithoutAvatarsAsync();
            return Ok(usersWithoutAvatars);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении пользователей без аватарок");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить количество пользователей без аватарок
    /// </summary>
    [HttpGet("without-avatars/count")]
    public async Task<ActionResult<object>> GetUsersWithoutAvatarsCount()
    {
        try
        {
            var count = await viewersService.GetUsersWithoutAvatarsCountAsync();
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при подсчете пользователей без аватарок");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Обновить аватарки для пользователей без них
    /// </summary>
    [HttpPost("update-avatars")]
    public async Task<ActionResult<object>> UpdateMissingAvatars()
    {
        try
        {
            var updatedCount = await viewersService.UpdateMissingAvatarsAsync();
            return Ok(new { 
                message = $"Обновлено {updatedCount} аватарок",
                updatedCount 
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении аватарок");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Тестовый endpoint для проверки обновления аватарок
    /// </summary>
    [HttpGet("debug/avatars")]
    public async Task<ActionResult<object>> DebugAvatars()
    {
        try
        {
            var usersWithoutAvatars = await viewersService.GetUsersWithoutAvatarsAsync();
            var count = await viewersService.GetUsersWithoutAvatarsCountAsync();
            
            var debugInfo = new
            {
                usersWithoutAvatarsCount = count,
                usersWithoutAvatars = usersWithoutAvatars.Take(5).Select(u => new
                {
                    userId = u.UserId,
                    userName = u.UserName,
                    profileImageUrl = u.ProfileImageUrl,
                    lastUpdated = u.LastUpdated
                }).ToList(),
                message = $"Найдено {count} пользователей без аватарок"
            };

            return Ok(debugInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении отладочной информации об аватарках");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
