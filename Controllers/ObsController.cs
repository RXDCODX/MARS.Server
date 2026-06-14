using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.Obs;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ObsController(IObsService obsService) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        return Ok(new { IsConnected = obsService.IsConnected, IsPaused = obsService.IsPaused });
    }

    [HttpPost("connect")]
    public async Task<ActionResult> Connect(CancellationToken cancellationToken)
    {
        await obsService.ConnectAsync(cancellationToken);
        return Ok(new { message = "Connected to OBS" });
    }

    [HttpPost("disconnect")]
    public async Task<ActionResult> Disconnect(CancellationToken cancellationToken)
    {
        await obsService.DisconnectAsync(cancellationToken);
        return Ok(new { message = "Disconnected from OBS" });
    }

    [HttpPost("screenshot")]
    public async Task<ActionResult> Screenshot(
        [FromQuery] string? sourceName,
        CancellationToken cancellationToken
    )
    {
        var path = await obsService.ScreenshotAsync(sourceName, cancellationToken);
        return Ok(new { screenshotPath = path });
    }

    [HttpPost("freeze")]
    public async Task<ActionResult> Freeze(CancellationToken cancellationToken)
    {
        var result = await obsService.FreezeAsync(cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("unfreeze")]
    public async Task<ActionResult> Unfreeze(CancellationToken cancellationToken)
    {
        var result = await obsService.UnfreezeAsync(cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("pause-scene")]
    public async Task<ActionResult> PauseScene(CancellationToken cancellationToken)
    {
        var result = await obsService.SwitchToPauseSceneAsync(cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("unpause-scene")]
    public async Task<ActionResult> UnpauseScene(CancellationToken cancellationToken)
    {
        var result = await obsService.SwitchFromPauseSceneAsync(cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("toggle")]
    public async Task<ActionResult> Toggle(
        [FromQuery] ObsPauseMode mode = ObsPauseMode.FreezeFrame,
        CancellationToken cancellationToken = default
    )
    {
        var result = await obsService.TogglePauseAsync(mode, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
