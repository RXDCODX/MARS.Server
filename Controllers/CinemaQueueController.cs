using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using MARS.Server.Services.CinemaQueue.Services;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CinemaQueueController(
    ICinemaQueueService cinemaQueueService,
    ILogger<CinemaQueueController> logger,
    IMediaMetadataService metadataService
) : ControllerBase
{
    /// <summary>
    /// Получить все элементы очереди
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CinemaMediaItemDto>>> GetAllMediaItems(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<IEnumerable<CinemaMediaItemDto>> result = StatusCode(500, "Internal server error");
        
        try
        {
            var items = await cinemaQueueService.GetAllMediaItemsAsync(cancellationToken);
            result = Ok(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all media items");
        }
        
        return result;
    }

    /// <summary>
    /// Получить элемент по ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CinemaMediaItemDto>> GetMediaItem(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<CinemaMediaItemDto> result = StatusCode(500, "Internal server error");
        
        try
        {
            var item = await cinemaQueueService.GetMediaItemByIdAsync(id, cancellationToken);
            result = item == null ? NotFound($"Media item with ID {id} not found") : Ok(item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting media item with ID: {Id}", id);
        }
        
        return result;
    }

    /// <summary>
    /// Получить следующий элемент для просмотра
    /// </summary>
    [HttpGet("next")]
    public async Task<ActionResult<CinemaMediaItemDto>> GetNextMediaItem(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<CinemaMediaItemDto> result = StatusCode(500, "Internal server error");
        
        try
        {
            var item = await cinemaQueueService.GetNextMediaItemAsync(cancellationToken);
            result = item == null ? NotFound("No next media item found") : Ok(item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting next media item");
        }
        
        return result;
    }

    /// <summary>
    /// Получить элементы по статусу
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<CinemaMediaItemDto>>> GetMediaItemsByStatus(
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<IEnumerable<CinemaMediaItemDto>> result = StatusCode(500, "Internal server error");
        
        try
        {
            var items = await cinemaQueueService.GetMediaItemsByStatusAsync(
                status,
                cancellationToken
            );
            result = Ok(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting media items by status: {Status}", status);
        }
        
        return result;
    }

    /// <summary>
    /// Создать новый элемент
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CinemaMediaItemDto>> CreateMediaItem(
        CreateMediaItemRequest? request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<CinemaMediaItemDto> result = StatusCode(500, "Internal server error");
        
        try
        {
            if (ModelState.IsValid)
            {
                // Если title или description не указаны, пытаемся получить их из URL
                if (
                    request != null
                    && (
                        string.IsNullOrWhiteSpace(request.Title)
                        || string.IsNullOrWhiteSpace(request.Description)
                    )
                    && !string.IsNullOrWhiteSpace(request.MediaUrl)
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

                var mediaItem = await cinemaQueueService.CreateMediaItemAsync(
                    request,
                    cancellationToken
                );
                result = CreatedAtAction(nameof(GetMediaItem), new { id = mediaItem.Id }, mediaItem);
            }
            else
            {
                result = BadRequest(ModelState);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating media item: {Title}", request?.Title);
        }
        
        return result;
    }

    /// <summary>
    /// Обновить элемент
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CinemaMediaItemDto>> UpdateMediaItem(
        Guid id,
        UpdateMediaItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<CinemaMediaItemDto> result = StatusCode(500, "Internal server error");
        
        try
        {
            if (ModelState.IsValid)
            {
                var mediaItem = await cinemaQueueService.UpdateMediaItemAsync(
                    id,
                    request,
                    cancellationToken
                );
                result = mediaItem == null
                    ? NotFound($"Media item with ID {id} not found")
                    : Ok(mediaItem);
            }
            else
            {
                result = BadRequest(ModelState);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating media item with ID: {Id}", id);
        }
        
        return result;
    }

    /// <summary>
    /// Удалить элемент
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteMediaItem(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult result = StatusCode(500, "Internal server error");
        
        try
        {
            var deleteResult = await cinemaQueueService.DeleteMediaItemAsync(id, cancellationToken);
            result = !deleteResult ? NotFound($"Media item with ID {id} not found") : NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting media item with ID: {Id}", id);
        }
        
        return result;
    }

    /// <summary>
    /// Отметить элемент как следующий для просмотра
    /// </summary>
    [HttpPost("{id:guid}/mark-as-next")]
    public async Task<ActionResult> MarkAsNext(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult result = StatusCode(500, "Internal server error");
        
        try
        {
            var markResult = await cinemaQueueService.MarkAsNextAsync(id, cancellationToken);
            result = !markResult
                ? NotFound($"Media item with ID {id} not found")
                : Ok(new { message = "Media item marked as next successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error marking media item as next with ID: {Id}", id);
        }
        
        return result;
    }

    /// <summary>
    /// Изменить статус элемента
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult> ChangeStatus(
        Guid id,
        [FromBody] MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult result = StatusCode(500, "Internal server error");
        
        try
        {
            var statusResult = await cinemaQueueService.ChangeStatusAsync(id, status, cancellationToken);
            result = !statusResult
                ? NotFound($"Media item with ID {id} not found")
                : Ok(new { message = $"Status changed to {status} successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing status of media item with ID: {Id}", id);
        }
        
        return result;
    }

    /// <summary>
    /// Изменить приоритет элемента
    /// </summary>
    [HttpPatch("{id:guid}/priority")]
    public async Task<ActionResult> ChangePriority(
        Guid id,
        [FromBody] int priority,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult result = StatusCode(500, "Internal server error");
        
        try
        {
            var priorityResult = await cinemaQueueService.ChangePriorityAsync(
                id,
                priority,
                cancellationToken
            );
            result = !priorityResult
                ? NotFound($"Media item with ID {id} not found")
                : Ok(new { message = $"Priority changed to {priority} successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing priority of media item with ID: {Id}", id);
        }
        
        return result;
    }

    /// <summary>
    /// Получить статистику очереди
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<CinemaQueueStatistics>> GetStatistics(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<CinemaQueueStatistics> result = StatusCode(500, "Internal server error");
        
        try
        {
            var stats = await cinemaQueueService.GetStatisticsAsync(cancellationToken);
            result = Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cinema queue statistics");
        }
        
        return result;
    }

    /// <summary>
    /// Получить метаданные из URL (Кинопоиск или Шикимори)
    /// </summary>
    [HttpGet("metadata")]
    public async Task<ActionResult<MediaMetadata>> GetMetadata(
        [FromQuery] string url,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<MediaMetadata> result = StatusCode(500, "Internal server error");
        
        try
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                var metadata = await metadataService.GetMetadataAsync(url, cancellationToken);
                result = metadata == null
                    ? NotFound("Metadata not found for the provided URL")
                    : Ok(metadata);
            }
            else
            {
                result = BadRequest("URL parameter is required");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting metadata for URL: {Url}", url);
        }
        
        return result;
    }
}
