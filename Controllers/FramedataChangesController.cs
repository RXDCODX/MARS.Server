using MARS.Server.Services.Framedata;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для управления staging-изменениями фреймдаты
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FramedataChangesController(FramedataStagingService stagingService) : ControllerBase
{
    // Pending lists
    [HttpGet("pending/characters")]
    public async Task<ActionResult<List<TekkenCharacterPendingDto>>> GetPendingCharacters() =>
        Ok(await stagingService.GetPendingCharacters());

    [HttpGet("pending/moves")]
    public async Task<ActionResult<List<MovePendingDto>>> GetPendingMoves(
        [FromQuery] string? characterName
    ) => Ok(await stagingService.GetPendingMoves(characterName));

    // Approve/Reject single entities
    [HttpPost("approve/character/{name}")]
    public async Task<ActionResult> ApproveCharacter(string name)
    {
        await stagingService.ApproveCharacter(name);
        return Ok(new { message = "Персонаж применён" });
    }

    [HttpPost("reject/character/{name}")]
    public async Task<ActionResult> RejectCharacter(string name)
    {
        await stagingService.RejectCharacter(name);
        return Ok(new { message = "Персонаж отклонён" });
    }

    [HttpPost("approve/move/{characterName}/{command}")]
    public async Task<ActionResult> ApproveMove(string characterName, string command)
    {
        await stagingService.ApproveMove(characterName, command);
        return Ok(new { message = "Ход применён" });
    }

    [HttpPost("reject/move/{characterName}/{command}")]
    public async Task<ActionResult> RejectMove(string characterName, string command)
    {
        await stagingService.RejectMove(characterName, command);
        return Ok(new { message = "Ход отклонён" });
    }

    // Approve/Reject all
    [HttpPost("approve/all")]
    public async Task<ActionResult> ApproveAll()
    {
        await stagingService.ApproveAll();
        return Ok(new { message = "Все изменения применены" });
    }

    [HttpPost("reject/all")]
    public async Task<ActionResult> RejectAll()
    {
        await stagingService.RejectAll();
        return Ok(new { message = "Все изменения отклонены" });
    }

    // Триггер обычного парсинга -> складывает в staging
    [HttpPost("scrape")]
    public async Task<ActionResult> Scrape([FromServices] Tekken8FrameData frameData)
    {
        await frameData.StartScrupFrameData();
        return Ok(new { message = "Парсинг запущен, изменения отправлены в staging" });
    }

    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStats()
    {
        var pendingChars = await stagingService.GetPendingCharacters();
        var pendingMoves = await stagingService.GetPendingMoves();
        var stats = new
        {
            PendingCharacters = pendingChars.Count,
            PendingMoves = pendingMoves.Count,
            ByCharacter = pendingMoves
                .GroupBy(m => m.CharacterName)
                .ToDictionary(g => g.Key, g => g.Count()),
        };
        return Ok(stats);
    }
}
