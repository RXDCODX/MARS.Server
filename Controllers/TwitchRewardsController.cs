using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Management;
using Microsoft.AspNetCore.Mvc;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomRewardRedemption;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;

namespace MARS.Server.Controllers;

/// <summary>
/// Управление наградами Twitch (Channel Points) через ITwitch API
/// </summary>
[ApiController]
[Route("api/twitch/rewards")]
public class TwitchRewardsController(
    ITwitchAPI api,
    TokenService tokenService,
    ILogger<TwitchRewardsController> logger
) : ControllerBase
{
    private string? AccessToken => tokenService.Token!.AccessToken;

    /// <summary>
    /// Получить список наград канала
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRewards([FromQuery] bool onlyManageable = true)
    {
        try
        {
            var response = await api.Helix.ChannelPoints.GetCustomRewardAsync(
                TwitchExstension.ChannelId,
                null,
                onlyManageable,
                AccessToken
            );
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return StatusCode(500, "Не удалось получить награды");
        }
    }

    /// <summary>
    /// Создать новую награду
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CustomReward>> CreateReward(
        [FromBody] CreateCustomRewardsRequest request
    )
    {
        try
        {
            var created = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(
                TwitchExstension.ChannelId,
                request,
                AccessToken
            );
            return Ok(created.Data.FirstOrDefault());
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return StatusCode(500, "Не удалось создать награду");
        }
    }

    /// <summary>
    /// Обновить существующую награду
    /// </summary>
    [HttpPatch("{rewardId}")]
    public async Task<ActionResult<CustomReward>> UpdateReward(
        string rewardId,
        [FromBody] UpdateCustomRewardRequest request
    )
    {
        try
        {
            var updated = await api.Helix.ChannelPoints.UpdateCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                request,
                AccessToken
            );
            return Ok(updated.Data.FirstOrDefault());
        }
        catch (TwitchLib.Api.Core.Exceptions.BadRequestException bre)
        {
            // Happens if награда не управляется приложением/неподходящие поля
            logger.LogWarning(bre, "BadRequest при обновлении награды {RewardId}", rewardId);
            return BadRequest(bre.Message);
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return StatusCode(500, "Не удалось обновить награду");
        }
    }

    /// <summary>
    /// Удалить награду
    /// </summary>
    [HttpDelete("{rewardId}")]
    public async Task<ActionResult> DeleteReward(string rewardId)
    {
        try
        {
            await api.Helix.ChannelPoints.DeleteCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                AccessToken
            );
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return StatusCode(500, "Не удалось удалить награду");
        }
    }

    /// <summary>
    /// Получить активные/ожидающие списания (редемпшены) по награде
    /// </summary>
    [HttpGet("{rewardId}/redemptions")]
    public async Task<ActionResult<GetCustomRewardRedemptionResponse>> GetRedemptions(
        string rewardId,
        [FromQuery] string status = "UNFULFILLED",
        [FromQuery] string? sort = null,
        [FromQuery] string? after = null,
        [FromQuery] int first = 50
    )
    {
        try
        {
            var response = await api.Helix.ChannelPoints.GetCustomRewardRedemptionAsync(
                TwitchExstension.ChannelId,
                rewardId,
                [status],
                sort,
                after,
                AccessToken
            );
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return StatusCode(500, "Не удалось получить редемпшены");
        }
    }

    /// <summary>
    /// Обновить статус редемпшенов (FULFILLED/CANCELED)
    /// </summary>
    [HttpPost("{rewardId}/redemptions/status")]
    public async Task<ActionResult> UpdateRedemptionStatus(
        string rewardId,
        [FromBody] UpdateCustomRewardRedemptionStatusRequest request,
        [FromQuery] List<string> ids
    )
    {
        if (ids == null || ids.Count == 0)
        {
            return BadRequest("Не указаны идентификаторы редемпшенов");
        }

        try
        {
            await api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                TwitchExstension.ChannelId,
                rewardId,
                ids,
                request,
                AccessToken
            );
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return StatusCode(500, "Не удалось обновить статус редемпшенов");
        }
    }
}
