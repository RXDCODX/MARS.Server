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
    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] bool forceUseCash)
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
}
