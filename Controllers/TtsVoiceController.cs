using MARS.Server.Services;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/tts")]
public class TtsVoiceController(
    IVoicer voicer,
    ITtsVoiceRepository repository,
    ILogger<TtsVoiceController> logger
) : ControllerBase
{
    [HttpGet("blocked")]
    public async Task<ActionResult<OperationResult<List<string>>>> GetBlocked(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var blocked = await repository.GetBlockedVoicesAsync(cancellationToken);
            return Ok(OperationResult<List<string>>.Ok("Список заблокированных голосов", blocked));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении заблокированных голосов");
            return Ok(OperationResult<List<string>>.Bad("Не удалось получить список", []));
        }
    }

    [HttpPost("blocked")]
    public async Task<ActionResult<OperationResult>> BlockVoice(
        [FromBody] UpdateVoiceRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.VoiceName))
        {
            return Ok(OperationResult.Bad("Имя голоса не может быть пустым"));
        }

        try
        {
            var added = await repository.AddBlockedVoiceAsync(request.VoiceName, cancellationToken);
            if (added)
            {
                await voicer.RefreshBlockedVoicesAsync(cancellationToken);
                return Ok(OperationResult.Ok("Голос заблокирован"));
            }

            return Ok(OperationResult.Bad("Голос уже заблокирован или имя некорректно"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при блокировке голоса {Voice}", request.VoiceName);
            return Ok(OperationResult.Bad("Не удалось заблокировать голос"));
        }
    }

    [HttpDelete("blocked/{voiceName}")]
    public async Task<ActionResult<OperationResult>> UnblockVoice(
        [FromRoute] string voiceName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(voiceName))
        {
            return Ok(OperationResult.Bad("Имя голоса не может быть пустым"));
        }

        try
        {
            var removed = await repository.RemoveBlockedVoiceAsync(voiceName, cancellationToken);
            if (removed)
            {
                await voicer.RefreshBlockedVoicesAsync(cancellationToken);
                return Ok(OperationResult.Ok("Голос разблокирован"));
            }

            return Ok(OperationResult.Bad("Голос не найден в списке блокировок"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при разблокировке голоса {Voice}", voiceName);
            return Ok(OperationResult.Bad("Не удалось разблокировать голос"));
        }
    }

    [HttpGet("installed")]
    public async Task<ActionResult<OperationResult<List<string>>>> GetInstalled(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var voices = await voicer.GetInstalledVoicesAsync(cancellationToken);
            return Ok(OperationResult<List<string>>.Ok("Доступные голоса", voices));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении списка голосов");
            return Ok(OperationResult<List<string>>.Bad("Не удалось получить список голосов", []));
        }
    }

    [HttpGet("linked")]
    public async Task<ActionResult<OperationResult<Dictionary<string, string>>>> GetLinked(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var linked = await voicer.GetLinkedVoicesAsync(cancellationToken);
            return Ok(
                OperationResult<Dictionary<string, string>>.Ok(
                    "Назначенные голоса",
                    new Dictionary<string, string>(linked)
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении назначенных голосов");
            return Ok(
                OperationResult<Dictionary<string, string>>.Bad(
                    "Не удалось получить назначенные голоса",
                    []
                )
            );
        }
    }

    [HttpPost("reset/{userName}")]
    public async Task<ActionResult<OperationResult>> ResetUserVoice(
        [FromRoute] string userName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Ok(OperationResult.Bad("Имя пользователя не может быть пустым"));
        }

        try
        {
            await voicer.ResetVoiceAsync(userName, cancellationToken);
            return Ok(OperationResult.Ok("Голос пользователя сброшен"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при сбросе голоса пользователя {User}", userName);
            return Ok(OperationResult.Bad("Не удалось сбросить голос пользователя"));
        }
    }

    [HttpPost("reset-all")]
    public async Task<ActionResult<OperationResult>> ResetAllVoices(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await voicer.ResetAllVoicesAsync(cancellationToken);
            return Ok(OperationResult.Ok("Голоса всех пользователей сброшены"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при сбросе всех голосов");
            return Ok(OperationResult.Bad("Не удалось сбросить голоса"));
        }
    }

    [HttpPost("speak")]
    public async Task<ActionResult<OperationResult>> Speak(
        [FromBody] SpeakRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Message))
        {
            return Ok(OperationResult.Bad("Имя пользователя и текст сообщения обязательны"));
        }

        try
        {
            await voicer.Sound(
                new MessageToSynthezid
                {
                    Name = request.Name,
                    Message = request.Message,
                    CreationDateTime = DateTimeOffset.Now,
                    Guid = Guid.NewGuid(),
                }
            );

            return Ok(OperationResult.Ok("Сообщение отправлено на озвучку"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при озвучке сообщения пользователя {User}", request.Name);
            return Ok(OperationResult.Bad("Не удалось отправить сообщение на озвучку"));
        }
    }
}

public class UpdateVoiceRequest
{
    public string VoiceName { get; set; } = string.Empty;
}

public class SpeakRequest
{
    public string Name { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
