using MARS.Server.Services.TelegramBotService;
using MARS.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelegramClipboardCopyController(
    TelegramClipboardCopyService telegramClipboardCopyService
) : ControllerBase
{
    [HttpGet("{requestId}")]
    public async Task<ActionResult<OperationResult<string[]>>> GetFilesByRequestId(string requestId)
    {
        var result = OperationResult<string[]>.Bad(
            "ID запроса не передан",
            []
        );

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            result = await telegramClipboardCopyService.GetFileUrlsByRequestIdAsync(requestId);
        }

        return Ok(result);
    }

    [HttpPost("complete/{requestId}")]
    public async Task<ActionResult<OperationResult>> CompleteRequest(string requestId)
    {
        var result = await telegramClipboardCopyService.MarkRequestAsCompletedAsync(requestId);
        return Ok(result);
    }
}
