using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services;
using MARS.Server.Services.NSFWBooru;
using MARS.Server.Services.NSFWBooru.Entities;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NSFWBooruAutoPostController(
    INSFWBooruAutoPostService service,
    ILogger<NSFWBooruAutoPostController> logger
) : ControllerBase
{
    [HttpGet("configs")]
    public async Task<ActionResult<OperationResult<List<NSFWBooruAutoPostConfigDto>>>> GetAll(
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<List<NSFWBooruAutoPostConfigDto>>> result;

        try
        {
            var serviceResult = await service.GetAllAsync(cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения конфигураций NSFWBooruAutoPost");
            result = Ok(
                OperationResult<List<NSFWBooruAutoPostConfigDto>>.Bad(
                    "Ошибка получения конфигураций",
                    []
                )
            );
        }

        return result;
    }

    [HttpPost("configs")]
    public async Task<ActionResult<OperationResult<NSFWBooruAutoPostConfigDto>>> Create(
        NSFWBooruAutoPostCreateRequest request,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<NSFWBooruAutoPostConfigDto>> result;

        try
        {
            var serviceResult = await service.CreateAsync(request, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка создания конфигурации NSFWBooruAutoPost");
            result = Ok(
                OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                    "Ошибка создания конфигурации",
                    new NSFWBooruAutoPostConfigDto()
                )
            );
        }

        return result;
    }

    [HttpPut("configs/{id:guid}")]
    public async Task<ActionResult<OperationResult<NSFWBooruAutoPostConfigDto>>> Update(
        Guid id,
        NSFWBooruAutoPostUpdateRequest request,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<NSFWBooruAutoPostConfigDto>> result;

        try
        {
            request.Id = id;
            var serviceResult = await service.UpdateAsync(request, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обновления конфигурации NSFWBooruAutoPost {Id}", id);
            result = Ok(
                OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                    "Ошибка обновления конфигурации",
                    new NSFWBooruAutoPostConfigDto()
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
            logger.LogError(ex, "Ошибка удаления конфигурации NSFWBooruAutoPost {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка удаления конфигурации"));
        }

        return result;
    }

    [HttpPut("configs/{id:guid}/enabled")]
    public async Task<ActionResult<OperationResult<NSFWBooruAutoPostConfigDto>>> SetEnabled(
        Guid id,
        [FromBody] NSFWSetEnabledRequest request,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<NSFWBooruAutoPostConfigDto>> result;

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
            logger.LogError(ex, "Ошибка изменения состояния NSFWBooruAutoPost {Id}", id);
            result = Ok(
                OperationResult<NSFWBooruAutoPostConfigDto>.Bad(
                    "Ошибка изменения состояния",
                    new NSFWBooruAutoPostConfigDto()
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
            logger.LogError(ex, "Ошибка ручного триггера NSFWBooruAutoPost {Id}", id);
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
            logger.LogError(ex, "Ошибка получения Discord каналов для NSFWBooruAutoPost");
            result = Ok(
                OperationResult<List<DiscordChannelOptionDto>>.Bad(
                    "Ошибка получения Discord каналов",
                    []
                )
            );
        }

        return result;
    }

    public class NSFWSetEnabledRequest
    {
        public bool IsEnabled { get; set; }
    }
}
