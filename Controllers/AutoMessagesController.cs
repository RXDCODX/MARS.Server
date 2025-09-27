using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.DTOs;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для работы с автоматическими сообщениями Twitch
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AutoMessagesController(
    IAutoMessagesService autoMessagesService,
    ILogger<AutoMessagesController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить все автоматические сообщения
    /// </summary>
    /// <returns>Список всех автоматических сообщений</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AutoMessageDto>>> GetAllAutoMessages(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<IEnumerable<AutoMessageDto>> result = StatusCode(500, "Внутренняя ошибка сервера");

        try
        {
            var messages = await autoMessagesService.GetAllAutoMessagesAsync(cancellationToken);
            result = Ok(messages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении всех автоматических сообщений");
        }

        return result;
    }

    /// <summary>
    /// Получить автоматическое сообщение по ID
    /// </summary>
    /// <param name="id">ID автоматического сообщения</param>
    /// <returns>Автоматическое сообщение</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AutoMessageDto>> GetAutoMessage(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<AutoMessageDto> result = StatusCode(500, "Внутренняя ошибка сервера");

        try
        {
            var message = await autoMessagesService.GetAutoMessageByIdAsync(id, cancellationToken);
            result = message == null
                ? NotFound($"Автоматическое сообщение с ID {id} не найдено")
                : Ok(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении автоматического сообщения с ID: {Id}", id);
        }

        return result;
    }

    /// <summary>
    /// Создать новое автоматическое сообщение
    /// </summary>
    /// <param name="request">Данные для создания автоматического сообщения</param>
    /// <returns>Созданное автоматическое сообщение</returns>
    [HttpPost]
    public async Task<ActionResult<AutoMessageDto>> CreateAutoMessage(
        CreateAutoMessageRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<AutoMessageDto> result = StatusCode(500, "Внутренняя ошибка сервера");

        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                result = BadRequest("Текст сообщения не может быть пустым");
            }
            else
            {
                var message = await autoMessagesService.CreateAutoMessageAsync(request, cancellationToken);

                if (message.Id != Guid.Empty)
                {
                    result = CreatedAtAction(
                        nameof(GetAutoMessage),
                        new { id = message.Id },
                        message
                    );
                }
                else
                {
                    result = BadRequest("Не удалось создать автоматическое сообщение");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании автоматического сообщения");
        }

        return result;
    }

    /// <summary>
    /// Обновить автоматическое сообщение
    /// </summary>
    /// <param name="id">ID автоматического сообщения</param>
    /// <param name="request">Данные для обновления</param>
    /// <returns>Обновленное автоматическое сообщение</returns>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AutoMessageDto>> UpdateAutoMessage(
        Guid id,
        UpdateAutoMessageRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<AutoMessageDto> result = StatusCode(500, "Внутренняя ошибка сервера");

        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                result = BadRequest("Текст сообщения не может быть пустым");
            }
            else
            {
                var message = await autoMessagesService.UpdateAutoMessageAsync(id, request, cancellationToken);

                result = message == null
                    ? NotFound($"Автоматическое сообщение с ID {id} не найдено")
                    : Ok(message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении автоматического сообщения с ID: {Id}", id);
        }

        return result;
    }

    /// <summary>
    /// Удалить автоматическое сообщение
    /// </summary>
    /// <param name="id">ID автоматического сообщения</param>
    /// <returns>Результат удаления</returns>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAutoMessage(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult result = StatusCode(500, "Внутренняя ошибка сервера");

        try
        {
            var deleted = await autoMessagesService.DeleteAutoMessageAsync(id, cancellationToken);

            result = deleted
                ? NoContent()
                : NotFound($"Автоматическое сообщение с ID {id} не найдено");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении автоматического сообщения с ID: {Id}", id);
        }

        return result;
    }
}

