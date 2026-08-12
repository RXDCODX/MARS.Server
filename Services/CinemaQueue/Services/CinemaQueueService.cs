using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using MARS.Server.Services.Twitch;

namespace MARS.Server.Services.CinemaQueue.Services;

public class CinemaQueueService(
    ICinemaQueueRepository repository,
    ILogger<CinemaQueueService> logger,
    TwitchUserEnsureService twitchUserEnsureService
) : ICinemaQueueService
{
    public async Task<IEnumerable<CinemaMediaItemDto>> GetAllMediaItemsAsync(
        CancellationToken cancellationToken = default
    )
    {
        IEnumerable<CinemaMediaItemDto> result = [];

        try
        {
            var items = await repository.GetAllAsync(cancellationToken);
            result = items.Select(MapToDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all media items");
            throw;
        }

        return result;
    }

    public async Task<CinemaMediaItemDto?> GetMediaItemByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        CinemaMediaItemDto? result = null;

        if (id != Guid.Empty)
        {
            try
            {
                var item = await repository.GetByIdAsync(id, cancellationToken);
                if (item != null)
                {
                    result = MapToDto(item);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting media item by id: {Id}", id);
                throw;
            }
        }

        return result;
    }

    public async Task<CinemaMediaItemDto?> GetNextMediaItemAsync(
        CancellationToken cancellationToken = default
    )
    {
        CinemaMediaItemDto? result = null;

        try
        {
            var item = await repository.GetNextAsync(cancellationToken);
            if (item != null)
            {
                result = MapToDto(item);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting next media item");
            throw;
        }

        return result;
    }

    public async Task<IEnumerable<CinemaMediaItemDto>> GetMediaItemsByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        IEnumerable<CinemaMediaItemDto> result = [];

        try
        {
            var items = await repository.GetByStatusAsync(status, cancellationToken);
            result = items.Select(MapToDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting media items by status: {Status}", status);
            throw;
        }

        return result;
    }

    public async Task<CinemaMediaItemDto> CreateMediaItemAsync(
        CreateMediaItemRequest? request,
        CancellationToken cancellationToken = default
    )
    {
        CinemaMediaItemDto result = new() { MediaUrl = string.Empty };

        if (request != null && !string.IsNullOrWhiteSpace(request.MediaUrl))
        {
            try
            {
                // Гарантируем наличие пользователя в TwitchUsers, если указан TwitchUserId
                var validTwitchUserId = request.TwitchUserId;
                if (!string.IsNullOrWhiteSpace(request.TwitchUserId))
                {
                    try
                    {
                        await twitchUserEnsureService.EnsureUserExistsAsync(
                            request.TwitchUserId,
                            cancellationToken: cancellationToken
                        );
                        validTwitchUserId = request.TwitchUserId;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to ensure Twitch user {UserId} exists, clearing TwitchUserId",
                            request.TwitchUserId
                        );
                        validTwitchUserId = null;
                    }
                }

                var mediaItem = new CinemaMediaItem
                {
                    Title = request.Title,
                    Description = request.Description,
                    MediaUrl = request.MediaUrl,
                    Priority = request.Priority,
                    ScheduledFor = request.ScheduledFor,
                    TwitchUserId = validTwitchUserId,
                    Notes = request.Notes,
                    Status = MediaStatus.Pending,
                    IsNext = false,
                    CreatedAt = DateTime.Now,
                    LastModified = DateTime.Now,
                };

                var createdItem = await repository.CreateAsync(mediaItem, cancellationToken);
                logger.LogInformation(
                    "Created media item: {Title} with ID: {Id}",
                    createdItem.Title ?? "Untitled",
                    createdItem.Id
                );

                result = MapToDto(createdItem);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error creating media item: {Title}",
                    request.Title ?? "Untitled"
                );
                throw;
            }
        }

        return result;
    }

    public async Task<CinemaMediaItemDto?> UpdateMediaItemAsync(
        Guid id,
        UpdateMediaItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        CinemaMediaItemDto? result = null;

        if (id != Guid.Empty && request != null)
        {
            try
            {
                var existingItem = await repository.GetByIdAsync(id, cancellationToken);
                if (existingItem != null)
                {
                    // Обновляем только переданные поля
                    if (request.Title != null)
                    {
                        existingItem.Title = request.Title;
                    }

                    if (request.Description != null)
                    {
                        existingItem.Description = request.Description;
                    }

                    if (request.MediaUrl != null)
                    {
                        existingItem.MediaUrl = request.MediaUrl;
                    }

                    if (request.Status.HasValue)
                    {
                        existingItem.Status = request.Status.Value;
                    }

                    if (request.Priority.HasValue)
                    {
                        existingItem.Priority = request.Priority.Value;
                    }

                    if (request.ScheduledFor.HasValue)
                    {
                        existingItem.ScheduledFor = request.ScheduledFor.Value;
                    }

                    if (request.Notes != null)
                    {
                        existingItem.Notes = request.Notes;
                    }

                    if (request.IsNext.HasValue)
                    {
                        existingItem.IsNext = request.IsNext.Value;
                    }

                    existingItem.LastModified = DateTime.Now;

                    var updatedItem = await repository.UpdateAsync(existingItem, cancellationToken);
                    logger.LogInformation("Updated media item: {Id}", id);

                    if (updatedItem != null)
                    {
                        result = MapToDto(updatedItem);
                    }
                }
                else
                {
                    logger.LogWarning("Media item not found for update: {Id}", id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating media item: {Id}", id);
                throw;
            }
        }

        return result;
    }

    public async Task<bool> DeleteMediaItemAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = false;

        if (id != Guid.Empty)
        {
            try
            {
                result = await repository.DeleteAsync(id, cancellationToken);
                if (result)
                {
                    logger.LogInformation("Deleted media item: {Id}", id);
                }
                else
                {
                    logger.LogWarning("Media item not found for deletion: {Id}", id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting media item: {Id}", id);
                throw;
            }
        }

        return result;
    }

    public async Task<bool> MarkAsNextAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = false;

        if (id != Guid.Empty)
        {
            try
            {
                var existingItem = await repository.GetByIdAsync(id, cancellationToken);
                if (existingItem != null)
                {
                    // Сбрасываем флаг IsNext для всех элементов
                    await repository.ResetNextFlagsAsync(cancellationToken);

                    // Устанавливаем флаг IsNext для выбранного элемента
                    existingItem.IsNext = true;
                    existingItem.LastModified = DateTime.Now;

                    var updatedItem = await repository.UpdateAsync(existingItem, cancellationToken);
                    logger.LogInformation(
                        "Marked media item as next: {Id} - {Title}",
                        id,
                        existingItem.Title
                    );

                    result = updatedItem != null;
                }
                else
                {
                    logger.LogWarning("Media item not found for marking as next: {Id}", id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error marking media item as next: {Id}", id);
                throw;
            }
        }

        return result;
    }

    public async Task<bool> ChangeStatusAsync(
        Guid id,
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        var result = false;

        if (id != Guid.Empty)
        {
            try
            {
                var existingItem = await repository.GetByIdAsync(id, cancellationToken);
                if (existingItem != null)
                {
                    existingItem.Status = status;
                    existingItem.LastModified = DateTime.Now;

                    var updatedItem = await repository.UpdateAsync(existingItem, cancellationToken);
                    logger.LogInformation(
                        "Changed status of media item {Id} to {Status}",
                        id,
                        status
                    );

                    result = updatedItem != null;
                }
                else
                {
                    logger.LogWarning("Media item not found for status change: {Id}", id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error changing status of media item: {Id}", id);
                throw;
            }
        }

        return result;
    }

    public async Task<bool> ChangePriorityAsync(
        Guid id,
        int priority,
        CancellationToken cancellationToken = default
    )
    {
        var result = false;

        if (id != Guid.Empty)
        {
            try
            {
                var existingItem = await repository.GetByIdAsync(id, cancellationToken);
                if (existingItem != null)
                {
                    existingItem.Priority = priority;
                    existingItem.LastModified = DateTime.Now;

                    var updatedItem = await repository.UpdateAsync(existingItem, cancellationToken);
                    logger.LogInformation(
                        "Changed priority of media item {Id} to {Priority}",
                        id,
                        priority
                    );

                    result = updatedItem != null;
                }
                else
                {
                    logger.LogWarning("Media item not found for priority change: {Id}", id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error changing priority of media item: {Id}", id);
                throw;
            }
        }

        return result;
    }

    public async Task<CinemaQueueStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default
    )
    {
        CinemaQueueStatistics result;

        try
        {
            var stats = new CinemaQueueStatistics
            {
                PendingItems = await repository.GetCountByStatusAsync(
                    MediaStatus.Pending,
                    cancellationToken
                ),
                InProgressItems = await repository.GetCountByStatusAsync(
                    MediaStatus.InProgress,
                    cancellationToken
                ),
                CompletedItems = await repository.GetCountByStatusAsync(
                    MediaStatus.Completed,
                    cancellationToken
                ),
                CancelledItems = await repository.GetCountByStatusAsync(
                    MediaStatus.Cancelled,
                    cancellationToken
                ),
                PostponedItems = await repository.GetCountByStatusAsync(
                    MediaStatus.Postponed,
                    cancellationToken
                ),
            };

            stats.TotalItems =
                stats.PendingItems
                + stats.InProgressItems
                + stats.CompletedItems
                + stats.CancelledItems
                + stats.PostponedItems;

            result = stats;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cinema queue statistics");
            throw;
        }

        return result;
    }

    private static CinemaMediaItemDto MapToDto(CinemaMediaItem item)
    {
        return new CinemaMediaItemDto
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            MediaUrl = item.MediaUrl,
            Status = item.Status,
            Priority = item.Priority,
            CreatedAt = item.CreatedAt,
            ScheduledFor = item.ScheduledFor,
            TwitchUserId = item.TwitchUserId,
            Notes = item.Notes,
            IsNext = item.IsNext,
            LastModified = item.LastModified,
        };
    }
}
