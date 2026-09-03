using MARS.Server.Services;
using MARS.Server.Services.BooruAutoPost;
using MARS.Server.Services.BooruAutoPost.Entities;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using MARS.Server.Services.Telegram.WTelegram;
using Microsoft.AspNetCore.Mvc;
using TL;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooruAutoPostController(
    IBooruAutoPostService service,
    WTelegramClientService wTelegramClientService,
    ILogger<BooruAutoPostController> logger
) : ControllerBase
{
    [HttpGet("configs")]
    public async Task<
        ActionResult<OperationResult<List<BooruAutoPostConfigDto>>>
    > GetAll(CancellationToken cancellationToken)
    {
        ActionResult<OperationResult<List<BooruAutoPostConfigDto>>> result;

        try
        {
            var serviceResult = await service.GetAllAsync(null, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения конфигураций BooruAutoPost");
            result = Ok(
                OperationResult<List<BooruAutoPostConfigDto>>.Bad(
                    "Ошибка получения конфигураций",
                    []
                )
            );
        }

        return result;
    }

    [HttpPost("configs")]
    public async Task<
        ActionResult<OperationResult<BooruAutoPostConfigDto>>
    > Create(BooruAutoPostCreateRequest request, CancellationToken cancellationToken)
    {
        ActionResult<OperationResult<BooruAutoPostConfigDto>> result;

        try
        {
            var serviceResult = await service.CreateAsync(request, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка создания конфигурации BooruAutoPost");
            result = Ok(
                OperationResult<BooruAutoPostConfigDto>.Bad(
                    "Ошибка создания конфигурации",
                    new BooruAutoPostConfigDto()
                )
            );
        }

        return result;
    }

    [HttpPut("configs/{id:guid}")]
    public async Task<
        ActionResult<OperationResult<BooruAutoPostConfigDto>>
    > Update(Guid id, BooruAutoPostUpdateRequest request, CancellationToken cancellationToken)
    {
        ActionResult<OperationResult<BooruAutoPostConfigDto>> result;

        try
        {
            request.Id = id;
            var serviceResult = await service.UpdateAsync(request, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обновления конфигурации BooruAutoPost {Id}", id);
            result = Ok(
                OperationResult<BooruAutoPostConfigDto>.Bad(
                    "Ошибка обновления конфигурации",
                    new BooruAutoPostConfigDto()
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
            logger.LogError(ex, "Ошибка удаления конфигурации BooruAutoPost {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка удаления конфигурации"));
        }

        return result;
    }

    [HttpPut("configs/{id:guid}/enabled")]
    public async Task<
        ActionResult<OperationResult<BooruAutoPostConfigDto>>
    > SetEnabled(
        Guid id,
        [FromBody] BooruSetEnabledRequest request,
        CancellationToken cancellationToken
    )
    {
        ActionResult<OperationResult<BooruAutoPostConfigDto>> result;

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
            logger.LogError(ex, "Ошибка изменения состояния BooruAutoPost {Id}", id);
            result = Ok(
                OperationResult<BooruAutoPostConfigDto>.Bad(
                    "Ошибка изменения состояния",
                    new BooruAutoPostConfigDto()
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
            logger.LogError(ex, "Ошибка ручного триггера BooruAutoPost {Id}", id);
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
            logger.LogError(ex, "Ошибка получения Discord каналов для BooruAutoPost");
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
            var client = await wTelegramClientService.GetClientAsync(cancellationToken);
            var chats = await client.Messages_GetAllChats();
            var channels = chats
                .chats.Values.OfType<Channel>()
                .Where(channel => channel.admin_rights is not null)
                .Select(channel => new TelegramChannelOptionDto
                {
                    Id = (-1000000000000 - channel.id).ToString(),
                    Title = string.IsNullOrWhiteSpace(channel.title)
                        ? $"channel-{channel.id}"
                        : channel.title,
                })
                .OrderBy(e => e.Title)
                .ThenBy(e => e.Id)
                .ToList();

            result = Ok(
                OperationResult<List<TelegramChannelOptionDto>>.Ok(
                    "Telegram каналы получены",
                    channels
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения Telegram каналов для BooruAutoPost");
            result = Ok(
                OperationResult<List<TelegramChannelOptionDto>>.Bad(
                    "Ошибка получения Telegram каналов",
                    []
                )
            );
        }

        return result;
    }

    public class BooruSetEnabledRequest
    {
        public bool IsEnabled { get; set; }
    }
}
