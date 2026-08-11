using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services;
using MARS.Server.Services.DanbooruAutoPost;
using MARS.Server.Services.DanbooruAutoPost.Entities;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DanbooruAutoPostController(
    IDanbooruAutoPostService service,
    ILogger<DanbooruAutoPostController> logger
) : ControllerBase
{
    [HttpGet("configs")]
    public async Task<ActionResult<OperationResult<List<DanbooruAutoPostConfigDto>>>> GetAll(
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<List<DanbooruAutoPostConfigDto>>> result;

        try
        {
            var serviceResult = await service.GetAllAsync(cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения конфигураций DanbooruAutoPost");
            result = Ok(
                OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                    "Ошибка получения конфигураций",
                    []
                )
            );
        }

        return result;
    }

    [HttpPost("configs")]
    public async Task<ActionResult<OperationResult<DanbooruAutoPostConfigDto>>> Create(
        DanbooruAutoPostCreateRequest request,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<DanbooruAutoPostConfigDto>> result;

        try
        {
            var serviceResult = await service.CreateAsync(request, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка создания конфигурации DanbooruAutoPost");
            result = Ok(
                OperationResult<DanbooruAutoPostConfigDto>.Bad(
                    "Ошибка создания конфигурации",
                    new DanbooruAutoPostConfigDto()
                )
            );
        }

        return result;
    }

    [HttpPost("configs/batch")]
    public async Task<ActionResult<OperationResult<List<DanbooruAutoPostConfigDto>>>> BatchCreate(
        DanbooruAutoPostBatchCreateRequest request,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<List<DanbooruAutoPostConfigDto>>> result;

        try
        {
            var serviceResult = await service.BatchCreateAsync(request, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка пакетного создания DanbooruAutoPost");
            result = Ok(
                OperationResult<List<DanbooruAutoPostConfigDto>>.Bad(
                    "Ошибка пакетного создания",
                    []
                )
            );
        }

        return result;
    }

    [HttpPut("configs/batch/{batchId:guid}/reschedule")]
    public async Task<
        ActionResult<OperationResult<List<DanbooruAutoPostConfigDto>>>
    > RescheduleBatch(
        Guid batchId,
        DanbooruAutoPostRescheduleRequest request,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<List<DanbooruAutoPostConfigDto>>> result;

        try
        {
            var serviceResult = await service.RescheduleBatchAsync(
                batchId,
                request,
                cancellationToken
            );
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка перепланирования батча {BatchId}", batchId);
            result = Ok(
                OperationResult<List<DanbooruAutoPostConfigDto>>.Bad("Ошибка перепланирования", [])
            );
        }

        return result;
    }

    [HttpDelete("configs/batch/{batchId:guid}")]
    public async Task<ActionResult<OperationResult>> DeleteBatch(
        Guid batchId,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            var serviceResult = await service.DeleteBatchAsync(batchId, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка удаления батча {BatchId}", batchId);
            result = Ok(OperationResult.Bad("Ошибка удаления батча"));
        }

        return result;
    }

    [HttpPut("configs/{id:guid}")]
    public async Task<ActionResult<OperationResult<DanbooruAutoPostConfigDto>>> Update(
        Guid id,
        DanbooruAutoPostUpdateRequest request,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<DanbooruAutoPostConfigDto>> result;

        try
        {
            request.Id = id;
            var serviceResult = await service.UpdateAsync(request, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обновления конфигурации DanbooruAutoPost {Id}", id);
            result = Ok(
                OperationResult<DanbooruAutoPostConfigDto>.Bad(
                    "Ошибка обновления конфигурации",
                    new DanbooruAutoPostConfigDto()
                )
            );
        }

        return result;
    }

    [HttpDelete("configs/{id:guid}")]
    public async Task<ActionResult<OperationResult>> Delete(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            var serviceResult = await service.DeleteAsync(id, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка удаления конфигурации DanbooruAutoPost {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка удаления конфигурации"));
        }

        return result;
    }

    [HttpPut("configs/{id:guid}/enabled")]
    public async Task<ActionResult<OperationResult<DanbooruAutoPostConfigDto>>> SetEnabled(
        Guid id,
        [FromBody] SetEnabledRequest request,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<DanbooruAutoPostConfigDto>> result;

        try
        {
            var serviceResult = await service.SetEnabledAsync(
                id,
                request.IsEnabled,
                cancellationToken
            );
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка изменения состояния DanbooruAutoPost {Id}", id);
            result = Ok(
                OperationResult<DanbooruAutoPostConfigDto>.Bad(
                    "Ошибка изменения состояния",
                    new DanbooruAutoPostConfigDto()
                )
            );
        }

        return result;
    }

    [HttpPost("configs/{id:guid}/trigger")]
    public async Task<ActionResult<OperationResult>> TriggerNow(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            var serviceResult = await service.TriggerNowAsync(id, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка ручного триггера DanbooruAutoPost {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка ручного триггера"));
        }

        return result;
    }

    [HttpGet("discord-channels")]
    public async Task<
        ActionResult<OperationResult<List<DiscordChannelOptionDto>>>
    > GetDiscordChannels(CancellationToken cancellationToken)
    {
        ActionResult<OperationResult<List<DiscordChannelOptionDto>>> result;

        try
        {
            var serviceResult = await service.GetDiscordChannelsAsync(cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения Discord каналов для DanbooruAutoPost");
            result = Ok(
                OperationResult<List<DiscordChannelOptionDto>>.Bad(
                    "Ошибка получения Discord каналов",
                    []
                )
            );
        }

        return result;
    }

    [HttpGet("telegram-channels")]
    public async Task<
        ActionResult<OperationResult<List<TelegramChannelOptionDto>>>
    > GetTelegramChannels(CancellationToken cancellationToken)
    {
        ActionResult<OperationResult<List<TelegramChannelOptionDto>>> result;

        try
        {
            var serviceResult = await service.GetTelegramChannelsAsync(cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения Telegram каналов для DanbooruAutoPost");
            result = Ok(
                OperationResult<List<TelegramChannelOptionDto>>.Bad(
                    "Ошибка получения Telegram каналов",
                    []
                )
            );
        }

        return result;
    }

    public class SetEnabledRequest
    {
        public bool IsEnabled { get; set; }
    }
}
