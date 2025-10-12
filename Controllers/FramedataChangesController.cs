using MARS.Server.Services;
using MARS.Server.Services.Framedata;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для управления staging-изменениями фреймдаты
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FramedataChangesController(
    FramedataStagingService stagingService,
    ILogger<FramedataChangesController> logger
) : ControllerBase
{
    // Pending lists
    [HttpGet("pending/characters")]
    public async Task<
        ActionResult<OperationResult<List<TekkenCharacterPendingDto>>>
    > GetPendingCharacters()
    {
        ActionResult<OperationResult<List<TekkenCharacterPendingDto>>> result;
        try
        {
            var characters = await stagingService.GetPendingCharacters();
            result = Ok(
                OperationResult<List<TekkenCharacterPendingDto>>.Ok(
                    "Получены ожидающие персонажи",
                    characters
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении ожидающих персонажей");
            result = Ok(
                OperationResult<List<TekkenCharacterPendingDto>>.Bad(
                    "Ошибка при получении ожидающих персонажей",
                    []
                )
            );
        }

        return result;
    }

    [HttpGet("pending/moves")]
    public async Task<ActionResult<OperationResult<List<MovePendingDto>>>> GetPendingMoves(
        [FromQuery] string? characterName
    )
    {
        ActionResult<OperationResult<List<MovePendingDto>>> result;
        try
        {
            var moves = await stagingService.GetPendingMoves(characterName);
            result = Ok(OperationResult<List<MovePendingDto>>.Ok("Получены ожидающие ходы", moves));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении ожидающих ходов");
            result = Ok(
                OperationResult<List<MovePendingDto>>.Bad(
                    "Ошибка при получении ожидающих ходов",
                    []
                )
            );
        }

        return result;
    }

    // Approve/Reject single entities
    [HttpPost("approve/character/{name}")]
    public async Task<ActionResult<OperationResult>> ApproveCharacter(string name)
    {
        ActionResult<OperationResult> result;
        try
        {
            await stagingService.ApproveCharacter(name);
            result = Ok(OperationResult.Ok("Персонаж применён"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при применении персонажа {Name}", name);
            result = Ok(OperationResult.Bad("Ошибка при применении персонажа"));
        }

        return result;
    }

    [HttpPost("reject/character/{name}")]
    public async Task<ActionResult<OperationResult>> RejectCharacter(string name)
    {
        ActionResult<OperationResult> result;
        try
        {
            await stagingService.RejectCharacter(name);
            result = Ok(OperationResult.Ok("Персонаж отклонён"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отклонении персонажа {Name}", name);
            result = Ok(OperationResult.Bad("Ошибка при отклонении персонажа"));
        }

        return result;
    }

    [HttpPost("approve/move/{characterName}/{command}")]
    public async Task<ActionResult<OperationResult>> ApproveMove(
        string characterName,
        string command
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            await stagingService.ApproveMove(characterName, command);
            result = Ok(OperationResult.Ok("Ход применён"));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при применении хода {CharacterName}/{Command}",
                characterName,
                command
            );
            result = Ok(OperationResult.Bad("Ошибка при применении хода"));
        }

        return result;
    }

    [HttpPost("reject/move/{characterName}/{command}")]
    public async Task<ActionResult<OperationResult>> RejectMove(
        string characterName,
        string command
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            await stagingService.RejectMove(characterName, command);
            result = Ok(OperationResult.Ok("Ход отклонён"));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при отклонении хода {CharacterName}/{Command}",
                characterName,
                command
            );
            result = Ok(OperationResult.Bad("Ошибка при отклонении хода"));
        }

        return result;
    }

    // Approve/Reject all
    [HttpPost("approve/all")]
    public async Task<ActionResult<OperationResult>> ApproveAll()
    {
        ActionResult<OperationResult> result;
        try
        {
            await stagingService.ApproveAll();
            result = Ok(OperationResult.Ok("Все изменения применены"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при применении всех изменений");
            result = Ok(OperationResult.Bad("Ошибка при применении всех изменений"));
        }

        return result;
    }

    [HttpPost("reject/all")]
    public async Task<ActionResult<OperationResult>> RejectAll()
    {
        ActionResult<OperationResult> result;
        try
        {
            await stagingService.RejectAll();
            result = Ok(OperationResult.Ok("Все изменения отклонены"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отклонении всех изменений");
            result = Ok(OperationResult.Bad("Ошибка при отклонении всех изменений"));
        }

        return result;
    }

    // Триггер обычного парсинга -> складывает в staging
    [HttpPost("scrape")]
    public async Task<ActionResult<OperationResult>> Scrape(
        [FromServices] Tekken8FrameData frameData
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            await frameData.StartScrupFrameData();
            result = Ok(OperationResult.Ok("Парсинг запущен, изменения отправлены в staging"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при запуске парсинга");
            result = Ok(OperationResult.Bad("Ошибка при запуске парсинга"));
        }

        return result;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<OperationResult<object>>> GetStats()
    {
        ActionResult<OperationResult<object>> result = null!;

        try
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
            result = Ok(OperationResult<object>.Ok("Получена статистика", stats));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статистики");
            result = Ok(OperationResult<object>.Bad("Ошибка при получении статистики", new { }));
        }

        return result;
    }
}
