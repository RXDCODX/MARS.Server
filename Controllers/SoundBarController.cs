using MARS.Server.Services.Twitch.SoundBarService.Entitys;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SoundBarController(ISoundBar soundBar) : ControllerBase
{
    [HttpPost("mute")]
    public async Task<IActionResult> Mute([FromBody] MuteRequest request)
    {
        try
        {
            await soundBar.Mute([.. request.ProcessNames]);
            return Ok(new { success = true, message = "Audio muted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("unmute")]
    public async Task<IActionResult> Unmute()
    {
        try
        {
            await soundBar.Unmute();
            return Ok(new { success = true, message = "Audio unmuted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("bagcount")]
    public async Task<IActionResult> GetBagCount()
    {
        try
        {
            var bagCount = await soundBar.GetBagCount();
            return Ok(new { success = true, bagCount });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}

public class MuteRequest
{
    public List<string> ProcessNames { get; set; } = [];
}
