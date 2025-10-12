using MARS.Server.Services;
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
    public async Task<ActionResult<OperationResult<List<FollowerInfo>?>>> GetAll(
        [FromQuery] bool forceUseCash
    )
    {
        ActionResult<OperationResult<List<FollowerInfo>?>> result;
        try
        {
            var allUsers = await viewersService.GetAllFollowersInfo(forceUseCash);

            result = Ok(
                allUsers != null
                    ? OperationResult<List<FollowerInfo>?>.Ok("Получены все подписчики", allUsers)
                    : OperationResult<List<FollowerInfo>?>.Bad("Токен недоступен", null)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении всех пользователей");
            result = Ok(
                OperationResult<List<FollowerInfo>?>.Bad("Ошибка при получении пользователей", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Получить пользователей без аватарок
    /// </summary>
    [HttpGet("without-avatars")]
    public async Task<ActionResult<OperationResult<List<FollowerInfo>>>> GetUsersWithoutAvatars()
    {
        ActionResult<OperationResult<List<FollowerInfo>>> result;
        try
        {
            var usersWithoutAvatars = await viewersService.GetUsersWithoutAvatarsAsync();
            result = Ok(
                OperationResult<List<FollowerInfo>>.Ok(
                    "Получены пользователи без аватарок",
                    usersWithoutAvatars
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении пользователей без аватарок");
            result = Ok(
                OperationResult<List<FollowerInfo>>.Bad(
                    "Ошибка при получении пользователей без аватарок",
                    []
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить количество пользователей без аватарок
    /// </summary>
    [HttpGet("without-avatars/count")]
    public async Task<ActionResult<OperationResult<int>>> GetUsersWithoutAvatarsCount()
    {
        ActionResult<OperationResult<int>> result;
        try
        {
            var count = await viewersService.GetUsersWithoutAvatarsCountAsync();
            result = Ok(
                OperationResult<int>.Ok("Подсчитано количество пользователей без аватарок", count)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при подсчете пользователей без аватарок");
            result = Ok(
                OperationResult<int>.Bad("Ошибка при подсчете пользователей без аватарок", 0)
            );
        }

        return result;
    }

    /// <summary>
    /// Обновить аватарки для пользователей без них
    /// </summary>
    [HttpPost("update-avatars")]
    public async Task<ActionResult<OperationResult<int>>> UpdateMissingAvatars()
    {
        ActionResult<OperationResult<int>> result;
        try
        {
            var updatedCount = await viewersService.UpdateMissingAvatarsAsync();
            result = Ok(
                OperationResult<int>.Ok($"Обновлено {updatedCount} аватарок", updatedCount)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении аватарок");
            result = Ok(OperationResult<int>.Bad("Ошибка при обновлении аватарок", 0));
        }

        return result;
    }

    /// <summary>
    /// Тестовый endpoint для проверки обновления аватарок
    /// </summary>
    [HttpGet("debug/avatars")]
    public async Task<ActionResult<OperationResult<object>>> DebugAvatars()
    {
        ActionResult<OperationResult<object>> result = null!;

        try
        {
            var usersWithoutAvatars = await viewersService.GetUsersWithoutAvatarsAsync();
            var count = await viewersService.GetUsersWithoutAvatarsCountAsync();

            var debugInfo = new
            {
                usersWithoutAvatarsCount = count,
                usersWithoutAvatars = usersWithoutAvatars
                    .Take(5)
                    .Select(u => new
                    {
                        userId = u.UserId,
                        userName = u.UserName,
                        profileImageUrl = u.ProfileImageUrl,
                        lastUpdated = u.LastUpdated,
                    })
                    .ToList(),
                message = $"Найдено {count} пользователей без аватарок",
            };

            result = Ok(OperationResult<object>.Ok("Получена отладочная информация", debugInfo));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении отладочной информации об аватарках");
            result = Ok(
                OperationResult<object>.Bad("Ошибка при получении отладочной информации", new { })
            );
        }

        return result;
    }
}
