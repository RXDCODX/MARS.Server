using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.DTOs;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    public async Task<
        ActionResult<OperationResult<IEnumerable<AutoMessageDto>>>
    > GetAllAutoMessages(CancellationToken cancellationToken = default)
    {
        ActionResult<OperationResult<IEnumerable<AutoMessageDto>>> result;
        try
        {
            var messages = await autoMessagesService.GetAllAutoMessagesAsync(cancellationToken);
            result = Ok(
                OperationResult<IEnumerable<AutoMessageDto>>.Ok(
                    "Успешно получены все автоматические сообщения",
                    messages
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении всех автоматических сообщений");
            result = Ok(
                OperationResult<IEnumerable<AutoMessageDto>>.Bad(
                    "Ошибка при получении автоматических сообщений",
                    []
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить автоматическое сообщение по ID
    /// </summary>
    /// <param name="id">ID автоматического сообщения</param>
    /// <returns>Автоматическое сообщение</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperationResult<AutoMessageDto?>>> GetAutoMessage(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<AutoMessageDto?>> result;
        try
        {
            var message = await autoMessagesService.GetAutoMessageByIdAsync(id, cancellationToken);

            if (message != null)
            {
                result = Ok(
                    OperationResult<AutoMessageDto?>.Ok("Автоматическое сообщение найдено", message)
                );
            }
            else
            {
                result = Ok(
                    OperationResult<AutoMessageDto?>.Bad(
                        $"Автоматическое сообщение с ID {id} не найдено",
                        null
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении автоматического сообщения с ID: {Id}", id);
            result = Ok(
                OperationResult<AutoMessageDto?>.Bad(
                    "Ошибка при получении автоматического сообщения",
                    null
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Создать новое автоматическое сообщение
    /// </summary>
    /// <param name="request">Данные для создания автоматического сообщения</param>
    /// <returns>Созданное автоматическое сообщение</returns>
    [HttpPost]
    public async Task<ActionResult<OperationResult<AutoMessageDto?>>> CreateAutoMessage(
        CreateAutoMessageRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<AutoMessageDto?>> result;
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                result = Ok(
                    OperationResult<AutoMessageDto?>.Bad(
                        "Текст сообщения не может быть пустым",
                        null
                    )
                );
            }
            else
            {
                var message = await autoMessagesService.CreateAutoMessageAsync(
                    request,
                    cancellationToken
                );

                if (message.Id != Guid.Empty)
                {
                    result = Ok(
                        OperationResult<AutoMessageDto?>.Ok(
                            "Автоматическое сообщение успешно создано",
                            message
                        )
                    );
                }
                else
                {
                    result = Ok(
                        OperationResult<AutoMessageDto?>.Bad(
                            "Не удалось создать автоматическое сообщение",
                            null
                        )
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании автоматического сообщения");
            result = Ok(
                OperationResult<AutoMessageDto?>.Bad(
                    "Ошибка при создании автоматического сообщения",
                    null
                )
            );
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
    public async Task<ActionResult<OperationResult<AutoMessageDto?>>> UpdateAutoMessage(
        Guid id,
        UpdateAutoMessageRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<AutoMessageDto?>> result;
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                result = Ok(
                    OperationResult<AutoMessageDto?>.Bad(
                        "Текст сообщения не может быть пустым",
                        null
                    )
                );
            }
            else
            {
                var message = await autoMessagesService.UpdateAutoMessageAsync(
                    id,
                    request,
                    cancellationToken
                );

                if (message != null)
                {
                    result = Ok(
                        OperationResult<AutoMessageDto?>.Ok(
                            "Автоматическое сообщение успешно обновлено",
                            message
                        )
                    );
                }
                else
                {
                    result = Ok(
                        OperationResult<AutoMessageDto?>.Bad(
                            $"Автоматическое сообщение с ID {id} не найдено",
                            null
                        )
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении автоматического сообщения с ID: {Id}", id);
            result = Ok(
                OperationResult<AutoMessageDto?>.Bad(
                    "Ошибка при обновлении автоматического сообщения",
                    null
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Удалить автоматическое сообщение
    /// </summary>
    /// <param name="id">ID автоматического сообщения</param>
    /// <returns>Результат удаления</returns>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<OperationResult>> DeleteAutoMessage(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            var deleted = await autoMessagesService.DeleteAutoMessageAsync(id, cancellationToken);

            if (deleted)
            {
                result = Ok(OperationResult.Ok("Автоматическое сообщение успешно удалено"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"Автоматическое сообщение с ID {id} не найдено"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении автоматического сообщения с ID: {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при удалении автоматического сообщения"));
        }

        return result;
    }
}
