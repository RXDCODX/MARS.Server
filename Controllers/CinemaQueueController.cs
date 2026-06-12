using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using MARS.Server.Services.CinemaQueue.Services;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CinemaQueueController(
    ICinemaQueueService cinemaQueueService,
    ILogger<CinemaQueueController> logger,
    IMediaMetadataService metadataService,
    IDbContextFactory<AppDbContext> dbFactory
) : ControllerBase
{
    /// <summary>
    /// Получить все элементы очереди
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<OperationResult<List<CinemaMediaItemDto>>>> GetAllMediaItems(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<CinemaMediaItemDto>>> result;
        try
        {
            var items = await cinemaQueueService.GetAllMediaItemsAsync(cancellationToken);
            result = Ok(
                OperationResult<List<CinemaMediaItemDto>>.Ok(
                    "Получены все элементы очереди",
                    items.ToList()
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all media items");
            result = Ok(
                OperationResult<List<CinemaMediaItemDto>>.Bad(
                    "Ошибка при получении элементов очереди",
                    []
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить элемент по ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperationResult<CinemaMediaItemDto?>>> GetMediaItem(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<CinemaMediaItemDto?>> result;
        try
        {
            var item = await cinemaQueueService.GetMediaItemByIdAsync(id, cancellationToken);

            if (item != null)
            {
                result = Ok(OperationResult<CinemaMediaItemDto?>.Ok("Элемент найден", item));
            }
            else
            {
                result = Ok(
                    OperationResult<CinemaMediaItemDto?>.Bad(
                        $"Media item with ID {id} not found",
                        null
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting media item with ID: {Id}", id);
            result = Ok(
                OperationResult<CinemaMediaItemDto?>.Bad("Ошибка при получении элемента", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Получить следующий элемент для просмотра
    /// </summary>
    [HttpGet("next")]
    public async Task<ActionResult<OperationResult<CinemaMediaItemDto?>>> GetNextMediaItem(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<CinemaMediaItemDto?>> result;
        try
        {
            var item = await cinemaQueueService.GetNextMediaItemAsync(cancellationToken);

            if (item != null)
            {
                result = Ok(
                    OperationResult<CinemaMediaItemDto?>.Ok("Следующий элемент найден", item)
                );
            }
            else
            {
                result = Ok(
                    OperationResult<CinemaMediaItemDto?>.Bad("No next media item found", null)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting next media item");
            result = Ok(
                OperationResult<CinemaMediaItemDto?>.Bad(
                    "Ошибка при получении следующего элемента",
                    null
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить элементы по статусу
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<
        ActionResult<OperationResult<List<CinemaMediaItemDto>>>
    > GetMediaItemsByStatus(MediaStatus status, CancellationToken cancellationToken = default)
    {
        ActionResult<OperationResult<List<CinemaMediaItemDto>>> result;
        try
        {
            var items = await cinemaQueueService.GetMediaItemsByStatusAsync(
                status,
                cancellationToken
            );
            result = Ok(
                OperationResult<List<CinemaMediaItemDto>>.Ok(
                    "Получены элементы по статусу",
                    items.ToList()
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting media items by status: {Status}", status);
            result = Ok(
                OperationResult<List<CinemaMediaItemDto>>.Bad(
                    "Ошибка при получении элементов по статусу",
                    []
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Создать новый элемент
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OperationResult<CinemaMediaItemDto?>>> CreateMediaItem(
        CreateMediaItemRequest? request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<CinemaMediaItemDto?>> result;
        try
        {
            if (request != null && !string.IsNullOrWhiteSpace(request.MediaUrl))
            {
                // Если title или description не указаны, пытаемся получить их из URL
                if (
                    string.IsNullOrWhiteSpace(request.Title)
                    || string.IsNullOrWhiteSpace(request.Description)
                )
                {
                    var metadata = await metadataService.GetMetadataAsync(
                        request.MediaUrl,
                        cancellationToken
                    );
                    if (metadata != null)
                    {
                        if (string.IsNullOrWhiteSpace(request.Title))
                        {
                            request.Title = metadata.Title;
                        }
                        if (string.IsNullOrWhiteSpace(request.Description))
                        {
                            request.Description = metadata.Description;
                        }
                    }
                }

                // Проверяем, существует ли пользователь в базе данных, если указан TwitchUserId
                if (!string.IsNullOrWhiteSpace(request.TwitchUserId))
                {
                    await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                    var userExists = await db
                        .TwitchUsers.AsNoTracking()
                        .AnyAsync(u => u.TwitchId == request.TwitchUserId, cancellationToken);

                    if (!userExists)
                    {
                        logger.LogWarning(
                            "Twitch user {UserId} not found in database, clearing TwitchUserId",
                            request.TwitchUserId
                        );
                        request.TwitchUserId = null;
                    }
                }

                var mediaItem = await cinemaQueueService.CreateMediaItemAsync(
                    request,
                    cancellationToken
                );
                result = Ok(
                    OperationResult<CinemaMediaItemDto?>.Ok("Элемент успешно создан", mediaItem)
                );
            }
            else
            {
                result = Ok(OperationResult<CinemaMediaItemDto?>.Bad("MediaUrl is required", null));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating media item: {Title}", request?.Title);
            result = Ok(
                OperationResult<CinemaMediaItemDto?>.Bad("Ошибка при создании элемента", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Обновить элемент
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OperationResult<CinemaMediaItemDto?>>> UpdateMediaItem(
        Guid id,
        UpdateMediaItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<CinemaMediaItemDto?>> result;
        try
        {
            if (ModelState.IsValid)
            {
                var mediaItem = await cinemaQueueService.UpdateMediaItemAsync(
                    id,
                    request,
                    cancellationToken
                );

                if (mediaItem != null)
                {
                    result = Ok(
                        OperationResult<CinemaMediaItemDto?>.Ok(
                            "Элемент успешно обновлен",
                            mediaItem
                        )
                    );
                }
                else
                {
                    result = Ok(
                        OperationResult<CinemaMediaItemDto?>.Bad(
                            $"Media item with ID {id} not found",
                            null
                        )
                    );
                }
            }
            else
            {
                result = Ok(
                    OperationResult<CinemaMediaItemDto?>.Bad("Некорректные данные модели", null)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating media item with ID: {Id}", id);
            result = Ok(
                OperationResult<CinemaMediaItemDto?>.Bad("Ошибка при обновлении элемента", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Удалить элемент
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<OperationResult>> DeleteMediaItem(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            var deleteResult = await cinemaQueueService.DeleteMediaItemAsync(id, cancellationToken);

            if (deleteResult)
            {
                result = Ok(OperationResult.Ok("Элемент успешно удален"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"Media item with ID {id} not found"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting media item with ID: {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при удалении элемента"));
        }

        return result;
    }

    /// <summary>
    /// Отметить элемент как следующий для просмотра
    /// </summary>
    [HttpPost("{id:guid}/mark-as-next")]
    public async Task<ActionResult<OperationResult>> MarkAsNext(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            var markResult = await cinemaQueueService.MarkAsNextAsync(id, cancellationToken);

            if (markResult)
            {
                result = Ok(OperationResult.Ok("Media item marked as next successfully"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"Media item with ID {id} not found"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error marking media item as next with ID: {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при отметке элемента как следующий"));
        }

        return result;
    }

    /// <summary>
    /// Изменить статус элемента
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<OperationResult>> ChangeStatus(
        Guid id,
        [FromBody] MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            var statusResult = await cinemaQueueService.ChangeStatusAsync(
                id,
                status,
                cancellationToken
            );

            if (statusResult)
            {
                result = Ok(OperationResult.Ok($"Status changed to {status} successfully"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"Media item with ID {id} not found"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing status of media item with ID: {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при изменении статуса элемента"));
        }

        return result;
    }

    /// <summary>
    /// Изменить приоритет элемента
    /// </summary>
    [HttpPatch("{id:guid}/priority")]
    public async Task<ActionResult<OperationResult>> ChangePriority(
        Guid id,
        [FromBody] int priority,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            var priorityResult = await cinemaQueueService.ChangePriorityAsync(
                id,
                priority,
                cancellationToken
            );

            if (priorityResult)
            {
                result = Ok(OperationResult.Ok($"Priority changed to {priority} successfully"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"Media item with ID {id} not found"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing priority of media item with ID: {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при изменении приоритета элемента"));
        }

        return result;
    }

    /// <summary>
    /// Получить статистику очереди
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<OperationResult<CinemaQueueStatistics?>>> GetStatistics(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<CinemaQueueStatistics?>> result;
        try
        {
            var stats = await cinemaQueueService.GetStatisticsAsync(cancellationToken);
            result = Ok(
                OperationResult<CinemaQueueStatistics?>.Ok("Получена статистика очереди", stats)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cinema queue statistics");
            result = Ok(
                OperationResult<CinemaQueueStatistics?>.Bad("Ошибка при получении статистики", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Получить метаданные из URL (Кинопоиск или Шикимори)
    /// </summary>
    [HttpGet("metadata")]
    public async Task<ActionResult<OperationResult<MediaMetadata?>>> GetMetadata(
        [FromQuery] string url,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<MediaMetadata?>> result;
        try
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                var metadata = await metadataService.GetMetadataAsync(url, cancellationToken);

                if (metadata != null)
                {
                    result = Ok(
                        OperationResult<MediaMetadata?>.Ok("Метаданные получены", metadata)
                    );
                }
                else
                {
                    result = Ok(
                        OperationResult<MediaMetadata?>.Bad(
                            "Metadata not found for the provided URL",
                            null
                        )
                    );
                }
            }
            else
            {
                result = Ok(OperationResult<MediaMetadata?>.Bad("URL parameter is required", null));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting metadata for URL: {Url}", url);
            result = Ok(
                OperationResult<MediaMetadata?>.Bad("Ошибка при получении метаданных", null)
            );
        }

        return result;
    }
}
