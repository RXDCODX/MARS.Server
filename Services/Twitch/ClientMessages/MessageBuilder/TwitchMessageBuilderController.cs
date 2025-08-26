using MARS.Server.Services.Twitch.ClientMessages.MessageBuilder.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Services.Twitch.ClientMessages.MessageBuilder;

/// <summary>
/// API контроллер для управления шаблонами сообщений Twitch
/// </summary>
[ApiController]
[Route("api/twitch/message-templates")]
public class TwitchMessageBuilderController(
    TwitchMessageBuilderService messageBuilderService,
    ILogger<TwitchMessageBuilderController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить все шаблоны сообщений
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TwitchMessageTemplateResponseDto>>> GetAllTemplates()
    {
        try
        {
            var templates = await messageBuilderService.GetAllTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении всех шаблонов");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить шаблон по ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TwitchMessageTemplateResponseDto>> GetTemplateById(Guid id)
    {
        try
        {
            var template = await messageBuilderService.GetTemplateByIdAsync(id);
            if (template == null)
                return NotFound($"Шаблон с ID {id} не найден");

            return Ok(template);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении шаблона {TemplateId}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Создать новый шаблон сообщения
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TwitchMessageTemplateResponseDto>> CreateTemplate(
        [FromBody] CreateTwitchMessageTemplateDto dto
    )
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var template = await messageBuilderService.CreateTemplateAsync(dto);
            return CreatedAtAction(nameof(GetTemplateById), new { id = template.Id }, template);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании шаблона");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Обновить существующий шаблон
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TwitchMessageTemplateResponseDto>> UpdateTemplate(
        Guid id,
        [FromBody] UpdateTwitchMessageTemplateDto dto
    )
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var template = await messageBuilderService.UpdateTemplateAsync(id, dto);
            if (template == null)
                return NotFound($"Шаблон с ID {id} не найден");

            return Ok(template);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении шаблона {TemplateId}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Удалить шаблон
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        try
        {
            var deleted = await messageBuilderService.DeleteTemplateAsync(id);
            if (!deleted)
                return NotFound($"Шаблон с ID {id} не найден");

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении шаблона {TemplateId}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить шаблоны по триггер-слову
    /// </summary>
    [HttpGet("by-trigger/{triggerWord}")]
    public async Task<ActionResult<List<TwitchMessageTemplateResponseDto>>> GetTemplatesByTrigger(
        string triggerWord
    )
    {
        try
        {
            var allTemplates = await messageBuilderService.GetAllTemplatesAsync();
            var templates = allTemplates
                .Where(t => t.TriggerWord.Equals(triggerWord, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Ok(templates);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении шаблонов по триггеру {TriggerWord}",
                triggerWord
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить активные шаблоны
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<TwitchMessageTemplateResponseDto>>> GetActiveTemplates()
    {
        try
        {
            var allTemplates = await messageBuilderService.GetAllTemplatesAsync();
            var activeTemplates = allTemplates.Where(t => t.IsActive).ToList();

            return Ok(activeTemplates);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении активных шаблонов");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить статистику использования шаблонов
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetTemplatesStats()
    {
        try
        {
            var allTemplates = await messageBuilderService.GetAllTemplatesAsync();

            var stats = new
            {
                TotalTemplates = allTemplates.Count,
                ActiveTemplates = allTemplates.Count(t => t.IsActive),
                InactiveTemplates = allTemplates.Count(t => !t.IsActive),
                TotalUsage = allTemplates.Sum(t => t.UsageCount),
                MostUsedTemplate = allTemplates
                    .OrderByDescending(t => t.UsageCount)
                    .FirstOrDefault(),
                TemplatesByPriority = allTemplates
                    .GroupBy(t => t.Priority)
                    .ToDictionary(g => g.Key, g => g.Count()),
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статистики шаблонов");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
}
