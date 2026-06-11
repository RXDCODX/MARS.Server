using MARS.Server.Services.Obs;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ObsController : ControllerBase
{
    private readonly IObsService _obsService;

    public ObsController(IObsService obsService)
    {
        _obsService = obsService;
    }

    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        return Ok(new { IsConnected = _obsService.IsConnected, IsPaused = _obsService.IsPaused });
    }

    [HttpPost("connect")]
    public async Task<ActionResult> Connect(CancellationToken cancellationToken)
    {
        await _obsService.ConnectAsync(cancellationToken);
        return Ok(new { message = "Connected to OBS" });
    }

    [HttpPost("disconnect")]
    public ActionResult Disconnect()
    {
        _obsService.DisconnectAsync();
        return Ok(new { message = "Disconnected from OBS" });
    }

    [HttpPost("screenshot")]
    public async Task<ActionResult> Screenshot(
        [FromQuery] string? sourceName,
        CancellationToken cancellationToken
    )
    {
        var path = await _obsService.ScreenshotAsync(sourceName, cancellationToken);
        return Ok(new { screenshotPath = path });
    }

    [HttpPost("freeze")]
    public async Task<ActionResult> Freeze(CancellationToken cancellationToken)
    {
        var result = await _obsService.FreezeAsync(cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("unfreeze")]
    public async Task<ActionResult> Unfreeze(CancellationToken cancellationToken)
    {
        var result = await _obsService.UnfreezeAsync(cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("pause-scene")]
    public async Task<ActionResult> PauseScene(CancellationToken cancellationToken)
    {
        var result = await _obsService.SwitchToPauseSceneAsync(cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("unpause-scene")]
    public async Task<ActionResult> UnpauseScene(CancellationToken cancellationToken)
    {
        var result = await _obsService.SwitchFromPauseSceneAsync(cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("toggle")]
    public async Task<ActionResult> Toggle(
        [FromQuery] ObsPauseMode mode = ObsPauseMode.FreezeFrame,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _obsService.TogglePauseAsync(mode, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
