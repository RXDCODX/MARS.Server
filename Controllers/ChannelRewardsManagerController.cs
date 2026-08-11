using MARS.Server.Exstensions;
using MARS.Server.Services;
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
    public async Task<ActionResult<OperationResult<IEnumerable<ChannelRewardRecord>>>> GetAll()
    {
        ActionResult<OperationResult<IEnumerable<ChannelRewardRecord>>> result;
        try
        {
            var rewards = await manager.GetAllAsync();
            result = Ok(
                OperationResult<IEnumerable<ChannelRewardRecord>>.Ok(
                    "Получены все награды",
                    rewards ?? []
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении всех наград");
            result = Ok(
                OperationResult<IEnumerable<ChannelRewardRecord>>.Bad(
                    "Ошибка при получении наград",
                    []
                )
            );
        }

        return result;
    }

    [HttpGet("local")]
    public async Task<ActionResult<OperationResult<IEnumerable<ChannelRewardRecord>>>> GetAllLocal()
    {
        ActionResult<OperationResult<IEnumerable<ChannelRewardRecord>>> result;
        try
        {
            var rewards = await manager.GetLocalAsync();
            result = Ok(
                OperationResult<IEnumerable<ChannelRewardRecord>>.Ok(
                    "Получены локальные награды",
                    rewards
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении локальных наград");
            result = Ok(
                OperationResult<IEnumerable<ChannelRewardRecord>>.Bad(
                    "Ошибка при получении локальных наград",
                    []
                )
            );
        }

        return result;
    }

    [HttpGet("local/{localId}")]
    public async Task<ActionResult<OperationResult<ChannelRewardRecord?>>> GetLocalById(
        [FromRoute] Guid localId
    )
    {
        ActionResult<OperationResult<ChannelRewardRecord?>> result;
        try
        {
            var reward = await manager.GetLocalByIdAsync(localId);

            if (reward != null)
            {
                result = Ok(
                    OperationResult<ChannelRewardRecord?>.Ok("Локальная награда найдена", reward)
                );
            }
            else
            {
                result = Ok(
                    OperationResult<ChannelRewardRecord?>.Bad(
                        $"Локальная награда с ID {localId} не найдена",
                        null
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении локальной награды {LocalId}", localId);
            result = Ok(
                OperationResult<ChannelRewardRecord?>.Bad(
                    "Ошибка при получении локальной награды",
                    null
                )
            );
        }

        return result;
    }

    [HttpGet("{rewardId}")]
    public async Task<ActionResult<OperationResult<ChannelRewardRecord?>>> GetById(
        [FromRoute] string rewardId
    )
    {
        ActionResult<OperationResult<ChannelRewardRecord?>> result;
        try
        {
            var reward = await manager.GetByIdAsync(rewardId);

            if (reward != null)
            {
                result = Ok(OperationResult<ChannelRewardRecord?>.Ok("Награда найдена", reward));
            }
            else
            {
                result = Ok(
                    OperationResult<ChannelRewardRecord?>.Bad(
                        $"Награда с ID {rewardId} не найдена",
                        null
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении награды {RewardId}", rewardId);
            result = Ok(
                OperationResult<ChannelRewardRecord?>.Bad("Ошибка при получении награды", null)
            );
        }

        return result;
    }

    [HttpPost("local")] // локальный upsert
    public async Task<ActionResult<OperationResult<ChannelRewardRecord?>>> UpsertLocal(
        [FromBody] ChannelRewardDefinition definition
    )
    {
        ActionResult<OperationResult<ChannelRewardRecord?>> result;
        try
        {
            var record = await manager.UpsertLocalAsync(definition);

            if (record != null)
            {
                result = Ok(
                    OperationResult<ChannelRewardRecord?>.Ok(
                        "Локальная награда успешно сохранена",
                        record
                    )
                );
            }
            else
            {
                result = Ok(
                    OperationResult<ChannelRewardRecord?>.Bad(
                        "Не удалось сохранить локальную награду",
                        null
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = Ok(
                OperationResult<ChannelRewardRecord?>.Bad(
                    $"Ошибка при сохранении локальной награды: {ex.Message}",
                    null
                )
            );
        }

        return result;
    }

    [HttpPut("local/{localId}")]
    public async Task<ActionResult<OperationResult>> UpdateLocal(
        [FromRoute] Guid localId,
        [FromBody] UpdateCustomRewardDto dto
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            var ok = await manager.UpdateLocalAsync(localId, dto);

            if (ok)
            {
                result = Ok(OperationResult.Ok("Локальная награда успешно обновлена"));
            }
            else
            {
                result = Ok(OperationResult.Bad("Не удалось обновить награду"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении локальной награды {LocalId}", localId);
            result = Ok(OperationResult.Bad("Ошибка при обновлении локальной награды"));
        }

        return result;
    }

    [HttpDelete("local/{localId}")]
    public async Task<ActionResult<OperationResult>> SoftDeleteLocal([FromRoute] Guid localId)
    {
        ActionResult<OperationResult> result;
        try
        {
            var ok = await manager.SoftDeleteLocalAsync(localId);

            if (ok)
            {
                result = Ok(OperationResult.Ok("Локальная награда успешно удалена"));
            }
            else
            {
                result = Ok(OperationResult.Bad("Не удалось удалить награду"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении локальной награды {LocalId}", localId);
            result = Ok(OperationResult.Bad("Ошибка при удалении локальной награды"));
        }

        return result;
    }

    [HttpPost("sync")]
    public async Task<ActionResult<OperationResult>> SyncNow(CancellationToken cancellationToken)
    {
        ActionResult<OperationResult> result;
        try
        {
            await syncService.SyncNow(cancellationToken);
            result = Ok(OperationResult.Ok("Синхронизация выполнена"));
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = Ok(OperationResult.Bad($"Ошибка синхронизации: {ex.Message}"));
        }

        return result;
    }

    [HttpPost("sync-services")]
    public async Task<ActionResult<OperationResult<int>>> SyncServicesToLocal()
    {
        ActionResult<OperationResult<int>> result;
        try
        {
            var count = await manager.SyncRewardServicesToLocalAsync();
            result = Ok(
                OperationResult<int>.Ok(
                    $"Синхронизировано {count} сервисов наград в локальную БД",
                    count
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = Ok(
                OperationResult<int>.Bad($"Ошибка синхронизации сервисов: {ex.Message}", 0)
            );
        }

        return result;
    }
}
