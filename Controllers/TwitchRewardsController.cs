using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;
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
    public async Task<ActionResult<OperationResult<GetCustomRewardsResponse?>>> GetRewards(
        [FromQuery] bool onlyManageable = true
    )
    {
        ActionResult<OperationResult<GetCustomRewardsResponse?>> result;
        try
        {
            var response = await api.Helix.ChannelPoints.GetCustomRewardAsync(
                TwitchExstension.ChannelId,
                null,
                onlyManageable,
                AccessToken
            );
            result = Ok(
                OperationResult<GetCustomRewardsResponse?>.Ok("Получены награды канала", response)
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = Ok(
                OperationResult<GetCustomRewardsResponse?>.Bad("Не удалось получить награды", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Создать новую награду
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OperationResult<CustomReward?>>> CreateReward(
        [FromBody] CreateCustomRewardsRequest request
    )
    {
        ActionResult<OperationResult<CustomReward?>> result;
        try
        {
            var created = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(
                TwitchExstension.ChannelId,
                request,
                AccessToken
            );
            var reward = created.Data.FirstOrDefault();
            result = Ok(OperationResult<CustomReward?>.Ok("Награда успешно создана", reward));
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = Ok(OperationResult<CustomReward?>.Bad("Не удалось создать награду", null));
        }

        return result;
    }

    /// <summary>
    /// Обновить существующую награду
    /// </summary>
    [HttpPatch("{rewardId}")]
    public async Task<ActionResult<OperationResult<CustomReward?>>> UpdateReward(
        string rewardId,
        [FromBody] UpdateCustomRewardRequest request
    )
    {
        ActionResult<OperationResult<CustomReward?>> result;
        try
        {
            var updated = await api.Helix.ChannelPoints.UpdateCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                request,
                AccessToken
            );
            var reward = updated.Data.FirstOrDefault();
            result = Ok(OperationResult<CustomReward?>.Ok("Награда успешно обновлена", reward));
        }
        catch (TwitchLib.Api.Core.Exceptions.BadRequestException bre)
        {
            // Happens if награда не управляется приложением/неподходящие поля
            logger.LogWarning(bre, "BadRequest при обновлении награды {RewardId}", rewardId);
            result = Ok(OperationResult<CustomReward?>.Bad(bre.Message, null));
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = Ok(OperationResult<CustomReward?>.Bad("Не удалось обновить награду", null));
        }

        return result;
    }

    /// <summary>
    /// Удалить награду
    /// </summary>
    [HttpDelete("{rewardId}")]
    public async Task<ActionResult<OperationResult>> DeleteReward(string rewardId)
    {
        ActionResult<OperationResult> result;
        try
        {
            await api.Helix.ChannelPoints.DeleteCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                AccessToken
            );
            result = Ok(OperationResult.Ok("Награда успешно удалена"));
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = Ok(OperationResult.Bad("Не удалось удалить награду"));
        }

        return result;
    }

    /// <summary>
    /// Получить активные/ожидающие списания (редемпшены) по награде
    /// </summary>
    [HttpGet("{rewardId}/redemptions")]
    public async Task<
        ActionResult<OperationResult<GetCustomRewardRedemptionResponse?>>
    > GetRedemptions(
        string rewardId,
        [FromQuery] string status = "UNFULFILLED",
        [FromQuery] string? sort = null,
        [FromQuery] string? after = null
    )
    {
        ActionResult<OperationResult<GetCustomRewardRedemptionResponse?>> result;
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
            result = Ok(
                OperationResult<GetCustomRewardRedemptionResponse?>.Ok(
                    "Получены редемпшены",
                    response
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = Ok(
                OperationResult<GetCustomRewardRedemptionResponse?>.Bad(
                    "Не удалось получить редемпшены",
                    null
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Обновить статус редемпшенов (FULFILLED/CANCELED)
    /// </summary>
    [HttpPost("{rewardId}/redemptions/status")]
    public async Task<ActionResult<OperationResult>> UpdateRedemptionStatus(
        string rewardId,
        [FromBody] UpdateCustomRewardRedemptionStatusRequest request,
        [FromQuery] List<string> ids
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            if (ids == null || ids.Count == 0)
            {
                result = Ok(OperationResult.Bad("Не указаны идентификаторы редемпшенов"));
            }
            else
            {
                await api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
                    TwitchExstension.ChannelId,
                    rewardId,
                    ids,
                    request,
                    AccessToken
                );
                result = Ok(OperationResult.Ok("Статус редемпшенов успешно обновлен"));
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = Ok(OperationResult.Bad("Не удалось обновить статус редемпшенов"));
        }

        return result;
    }
}
