using MARS.Server.Services;
using MARS.Server.Services.TtsMultilingual;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/tts/multilingual")]
public class MultilingualTtsController(
    IMultilingualTtsService multilingualTtsService,
    ILogger<MultilingualTtsController> logger
) : ControllerBase
{
    [HttpPost("synthesize")]
    public async Task<ActionResult> Synthesize(
        [FromBody] MultilingualTtsSynthesizeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult result = Ok(OperationResult.Bad("Не удалось выполнить синтез"));

        if (!string.IsNullOrWhiteSpace(request.Text))
        {
            try
            {
                var ttsResult = await multilingualTtsService.SynthesizeAsync(
                    request.Text,
                    request.Language,
                    request.Speaker,
                    cancellationToken
                );

                if (ttsResult.Success)
                {
                    result = File(ttsResult.Data.AudioBytes, ttsResult.Data.ContentType);
                }
                else
                {
                    result = Ok(OperationResult.Bad(ttsResult.Message));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в API мультиязычного синтеза");
                result = Ok(OperationResult.Bad($"Ошибка синтеза: {ex.Message}"));
            }
        }
        else
        {
            result = Ok(OperationResult.Bad("Текст для синтеза не может быть пустым"));
        }

        return result;
    }
}