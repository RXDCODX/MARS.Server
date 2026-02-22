using MARS.Server.Services;
using MARS.Server.Services.TelegramDiscordBridge;
using MARS.Server.Services.TelegramDiscordBridge.Entitys;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelegramDiscordBridgeController(
    ITelegramDiscordBridgeService bridgeService,
    ILogger<TelegramDiscordBridgeController> logger
) : ControllerBase
{
    [HttpGet("bindings")]
    public async Task<ActionResult<OperationResult<List<TelegramDiscordBindingDto>>>> GetBindings(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<TelegramDiscordBindingDto>>> result;

        try
        {
            var serviceResult = await bridgeService.GetBindingsAsync(cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения Telegram-Discord связей");
            result = Ok(
                OperationResult<List<TelegramDiscordBindingDto>>.Bad(
                    "Ошибка получения связей",
                    []
                )
            );
        }

        return result;
    }

    [HttpPost("bindings")]
    public async Task<ActionResult<OperationResult<TelegramDiscordBindingDto>>> AddBinding(
        TelegramDiscordBindingCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<TelegramDiscordBindingDto>> result;

        try
        {
            var serviceResult = await bridgeService.AddBindingAsync(request, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка добавления Telegram-Discord связи");
            result = Ok(
                OperationResult<TelegramDiscordBindingDto>.Bad(
                    "Ошибка добавления связи",
                    new TelegramDiscordBindingDto()
                )
            );
        }

        return result;
    }

    [HttpDelete("bindings/{id:guid}")]
    public async Task<ActionResult<OperationResult>> DeleteBinding(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            var serviceResult = await bridgeService.DeleteBindingAsync(id, cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка удаления Telegram-Discord связи {BindingId}", id);
            result = Ok(OperationResult.Bad("Ошибка удаления связи"));
        }

        return result;
    }

    [HttpPut("bindings/{id:guid}/enabled")]
    public async Task<ActionResult<OperationResult<TelegramDiscordBindingDto>>> SetBindingEnabled(
        Guid id,
        TelegramDiscordBindingSetEnabledRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<TelegramDiscordBindingDto>> result;

        try
        {
            var serviceResult = await bridgeService.SetBindingEnabledAsync(
                id,
                request.IsEnabled,
                cancellationToken
            );
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка обновления состояния Telegram-Discord связи {BindingId}",
                id
            );
            result = Ok(
                OperationResult<TelegramDiscordBindingDto>.Bad(
                    "Ошибка обновления состояния связи",
                    new TelegramDiscordBindingDto()
                )
            );
        }

        return result;
    }

    [HttpGet("states")]
    public async Task<ActionResult<OperationResult<List<TelegramDiscordChannelStateDto>>>> GetStates(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<TelegramDiscordChannelStateDto>>> result;

        try
        {
            var serviceResult = await bridgeService.GetStatesAsync(cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка чтения состояния Telegram-Discord bridge");
            result = Ok(
                OperationResult<List<TelegramDiscordChannelStateDto>>.Bad(
                    "Ошибка чтения состояния",
                    []
                )
            );
        }

        return result;
    }

    [HttpGet("telegram-channels")]
    public async Task<ActionResult<OperationResult<List<TelegramChannelOptionDto>>>> GetTelegramChannels(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<TelegramChannelOptionDto>>> result;

        try
        {
            var serviceResult = await bridgeService.GetTelegramChannelsAsync(cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения списка Telegram каналов для bridge");
            result = Ok(
                OperationResult<List<TelegramChannelOptionDto>>.Bad(
                    "Ошибка получения Telegram каналов",
                    []
                )
            );
        }

        return result;
    }

    [HttpGet("discord-channels")]
    public async Task<ActionResult<OperationResult<List<DiscordChannelOptionDto>>>> GetDiscordChannels(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<DiscordChannelOptionDto>>> result;

        try
        {
            var serviceResult = await bridgeService.GetDiscordChannelsAsync(cancellationToken);
            result = Ok(serviceResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения списка Discord каналов для bridge");
            result = Ok(
                OperationResult<List<DiscordChannelOptionDto>>.Bad(
                    "Ошибка получения Discord каналов",
                    []
                )
            );
        }

        return result;
    }
}
