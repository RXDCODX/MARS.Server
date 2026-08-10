using MARS.Server.Configuration;
using MARS.Server.Exstensions;
using MARS.Server.Services;
using MARS.Server.Services.Telegram.GooglePhotos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GooglePhotosController(
    GooglePhotosAuthService authService,
    ITelegramBotClient telegramBotClient,
    IOptions<GooglePhotosConfiguration> googlePhotosOptions,
    ILogger<GooglePhotosController> logger
) : ControllerBase
{
    private readonly GooglePhotosConfiguration _config = googlePhotosOptions.Value;

    [HttpPost("authorize")]
    public async Task<ActionResult<OperationResult<string>>> AuthorizeAsync(
        [FromQuery] long? telegramAdminId = null,
        CancellationToken ct = default
    )
    {
        if (!_config.Enabled)
        {
            logger.LogWarning("Google Photos disabled");
            return BadRequest(OperationResult<string>.Bad("Disabled"));
        }
        try
        {
            var authUrl = await authService.GetAuthorizationUrlAsync(ct);
            var adminId = telegramAdminId ?? _config.TelegramAdminId ?? TelegramExstension.Rxdcodx;
            await telegramBotClient.SendMessage(
                adminId,
                $"Auth: {authUrl}",
                parseMode: ParseMode.Html,
                cancellationToken: ct
            );
            logger.LogInformation("Auth link sent");
            return Ok(OperationResult<string>.Ok("Sent", authUrl));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auth error");
            return BadRequest(OperationResult<string>.Bad($"Error: {ex.Message}"));
        }
    }

    [HttpGet("oauth-callback")]
    public async Task<IActionResult> OAuthCallbackAsync(
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken ct = default
    )
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            logger.LogError("OAuth error: {Error}", error);
            return Ok($"Error: {error}");
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            logger.LogWarning("No code received");
            return Ok("No code");
        }

        var tokens = await authService.ExchangeCodeForTokenAsync(code, ct);
        if (tokens != null)
        {
            logger.LogInformation("Authorization successful");
            return Ok("Success");
        }
        logger.LogError("Failed to get tokens");
        return Ok("Failed");
    }

    [HttpGet("status")]
    public async Task<ActionResult<OperationResult<object>>> GetStatusAsync(
        CancellationToken ct = default
    )
    {
        var isAuthorized = await authService.IsAuthorizedAsync(ct);
        logger.LogInformation("Status check: {IsAuthorized}", isAuthorized);
        return Ok(
            OperationResult<object>.Ok(isAuthorized ? "OK" : "Not auth", new { isAuthorized })
        );
    }
}
