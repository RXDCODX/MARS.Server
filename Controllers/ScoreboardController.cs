using MARS.Server.Services.Scoreboard;
using MARS.Server.Services.Scoreboard.Entitys;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScoreboardController(ScoreboardService scoreboardService, ILogger<ScoreboardController> logger) : ControllerBase
{
    [HttpGet("test")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            var state = await scoreboardService.GetCurrentStateAsync();
            return Ok(new { 
                success = true, 
                message = "Database connection successful", 
                state = state 
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing scoreboard connection");
            return StatusCode(500, new { 
                success = false, 
                message = "Database connection failed", 
                error = ex.Message 
            });
        }
    }

    [HttpGet("state")]
    public async Task<IActionResult> GetCurrentState()
    {
        try
        {
            var state = await scoreboardService.GetCurrentStateAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current state");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("state")]
    public async Task<IActionResult> UpdateState([FromBody] ScoreboardDto state)
    {
        try
        {
            var updatedState = await scoreboardService.UpdateStateAsync(state);
            return Ok(updatedState);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating state");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("visibility")]
    public async Task<IActionResult> SetVisibility([FromBody] bool isVisible)
    {
        try
        {
            var success = await scoreboardService.SetVisibilityAsync(isVisible);
            return Ok(new { success });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting visibility");
            return StatusCode(500, new { error = ex.Message });
        }
    }
} 