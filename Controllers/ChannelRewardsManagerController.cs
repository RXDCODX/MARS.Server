using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Entities;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Models;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChannelRewardsManagerController(
    ChannelRewardsManager manager,
    ILogger<ChannelRewardsManagerController> logger,
    ChannelRewardsSyncService syncService
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rewards = await manager.GetAllAsync();
        return Ok(rewards ?? []);
    }

    [HttpGet("local")]
    public async Task<IActionResult> GetAllLocal()
    {
        var rewards = await manager.GetLocalAsync();
        return Ok(rewards);
    }

    [HttpGet("local/{localId}")]
    public async Task<IActionResult> GetLocalById([FromRoute] Guid localId)
    {
        var reward = await manager.GetLocalByIdAsync(localId);
        return reward != null ? Ok(reward) : NotFound();
    }

    [HttpGet("{rewardId}")]
    public async Task<IActionResult> GetById([FromRoute] string rewardId)
    {
        var reward = await manager.GetByIdAsync(rewardId);
        return reward != null ? Ok(reward) : NotFound();
    }

    [HttpPost("local")] // локальный upsert
    public async Task<IActionResult> UpsertLocal([FromBody] ChannelRewardRecord record)
    {
        ChannelRewardRecord? result;
        try
        {
            result = await manager.UpsertLocalAsync(record);
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return Problem(ex.Message);
        }

        return result == null ? Problem("Не удалось сохранить локальную награду") : Ok(result);
    }

    [HttpPut("local/{localId}")]
    public async Task<IActionResult> UpdateLocal(
        [FromRoute] Guid localId,
        [FromBody] UpdateCustomRewardDto dto
    )
    {
        var ok = await manager.UpdateLocalAsync(localId, dto);
        return ok ? Ok() : Problem("Не удалось обновить награду");
    }

    [HttpDelete("local/{localId}")]
    public async Task<IActionResult> SoftDeleteLocal([FromRoute] Guid localId)
    {
        var ok = await manager.SoftDeleteLocalAsync(localId);
        return ok ? NoContent() : Problem("Не удалось удалить награду");
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncNow(CancellationToken cancellationToken)
    {
        try
        {
            await syncService.SyncNow(cancellationToken);
            return Ok("Синхронизация выполнена");
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return Problem(ex.Message);
        }
    }

}
