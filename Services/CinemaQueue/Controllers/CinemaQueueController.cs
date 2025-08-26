using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Services.CinemaQueue.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CinemaQueueController(
    ICinemaQueueService cinemaQueueService,
    ILogger<CinemaQueueController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить все элементы очереди
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MediaItemDto>>> GetAllMediaItems(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var items = await cinemaQueueService.GetAllMediaItemsAsync(cancellationToken);
            return Ok(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all media items");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Получить элемент по ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MediaItemDto>> GetMediaItem(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var item = await cinemaQueueService.GetMediaItemByIdAsync(id, cancellationToken);
            return item == null
                ? NotFound($"Media item with ID {id} not found")
                : (ActionResult<MediaItemDto>)Ok(item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting media item with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Получить следующий элемент для просмотра
    /// </summary>
    [HttpGet("next")]
    public async Task<ActionResult<MediaItemDto>> GetNextMediaItem(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var item = await cinemaQueueService.GetNextMediaItemAsync(cancellationToken);
            return item == null ? NotFound("No next media item found") : Ok(item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting next media item");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Получить элементы по статусу
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<MediaItemDto>>> GetMediaItemsByStatus(
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var items = await cinemaQueueService.GetMediaItemsByStatusAsync(
                status,
                cancellationToken
            );
            return Ok(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting media items by status: {Status}", status);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Создать новый элемент
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MediaItemDto>> CreateMediaItem(
        CreateMediaItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var mediaItem = await cinemaQueueService.CreateMediaItemAsync(
                request,
                cancellationToken
            );
            return CreatedAtAction(nameof(GetMediaItem), new { id = mediaItem.Id }, mediaItem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating media item: {Title}", request.Title);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Обновить элемент
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MediaItemDto>> UpdateMediaItem(
        Guid id,
        UpdateMediaItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var mediaItem = await cinemaQueueService.UpdateMediaItemAsync(
                id,
                request,
                cancellationToken
            );
            return mediaItem == null
                ? NotFound($"Media item with ID {id} not found")
                : (ActionResult<MediaItemDto>)Ok(mediaItem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating media item with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
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
        try
        {
            var result = await cinemaQueueService.DeleteMediaItemAsync(id, cancellationToken);
            return !result ? NotFound($"Media item with ID {id} not found") : NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting media item with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
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
        try
        {
            var result = await cinemaQueueService.MarkAsNextAsync(id, cancellationToken);
            return !result
                ? NotFound($"Media item with ID {id} not found")
                : Ok(new { message = "Media item marked as next successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error marking media item as next with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
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
        try
        {
            var result = await cinemaQueueService.ChangeStatusAsync(id, status, cancellationToken);
            return !result
                ? NotFound($"Media item with ID {id} not found")
                : Ok(new { message = $"Status changed to {status} successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing status of media item with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
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
        try
        {
            var result = await cinemaQueueService.ChangePriorityAsync(
                id,
                priority,
                cancellationToken
            );
            return !result
                ? NotFound($"Media item with ID {id} not found")
                : Ok(new { message = $"Priority changed to {priority} successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing priority of media item with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Получить статистику очереди
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<CinemaQueueStatistics>> GetStatistics(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var stats = await cinemaQueueService.GetStatisticsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cinema queue statistics");
            return StatusCode(500, "Internal server error");
        }
    }
}
