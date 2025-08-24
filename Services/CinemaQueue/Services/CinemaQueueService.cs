using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using MediaType = MARS.Server.Services.CinemaQueue.Entitys.MediaType;

namespace MARS.Server.Services.CinemaQueue.Services;

public class CinemaQueueService : ICinemaQueueService
{
    private readonly ICinemaQueueRepository _repository;
    private readonly ILogger<CinemaQueueService> _logger;

    public CinemaQueueService(ICinemaQueueRepository repository, ILogger<CinemaQueueService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<MediaItemDto>> GetAllMediaItemsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var items = await _repository.GetAllAsync(cancellationToken);
            return items.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all media items");
            throw;
        }
    }

    public async Task<MediaItemDto?> GetMediaItemByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var item = await _repository.GetByIdAsync(id, cancellationToken);
            return item != null ? MapToDto(item) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media item by id: {Id}", id);
            throw;
        }
    }

    public async Task<MediaItemDto?> GetNextMediaItemAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var item = await _repository.GetNextAsync(cancellationToken);
            return item != null ? MapToDto(item) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting next media item");
            throw;
        }
    }

    public async Task<IEnumerable<MediaItemDto>> GetMediaItemsByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var items = await _repository.GetByStatusAsync(status, cancellationToken);
            return items.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media items by status: {Status}", status);
            throw;
        }
    }

    public async Task<IEnumerable<MediaItemDto>> GetMediaItemsByTypeAsync(
        MediaType type,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var items = await _repository.GetByTypeAsync(type, cancellationToken);
            return items.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media items by type: {Type}", type);
            throw;
        }
    }

    public async Task<MediaItemDto> CreateMediaItemAsync(
        CreateMediaItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var mediaItem = new MediaItem
            {
                Title = request.Title,
                Description = request.Description,
                Type = request.Type,
                Priority = request.Priority,
                ScheduledFor = request.ScheduledFor,
                AddedBy = request.AddedBy,
                TwitchUserId = request.TwitchUserId,
                TwitchUsername = request.TwitchUsername,
                Notes = request.Notes,
                EpisodeNumber = request.EpisodeNumber,
                Season = request.Season,
                Genre = request.Genre,
                PosterUrl = request.PosterUrl,
                DurationMinutes = request.DurationMinutes,
                Status = MediaStatus.Pending,
                IsNext = false,
                CreatedAt = DateTimeOffset.Now,
                LastModified = DateTimeOffset.Now,
            };

            var createdItem = await _repository.CreateAsync(mediaItem, cancellationToken);
            _logger.LogInformation(
                "Created media item: {Title} with ID: {Id}",
                createdItem.Title,
                createdItem.Id
            );

            return MapToDto(createdItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating media item: {Title}", request.Title);
            throw;
        }
    }

    public async Task<MediaItemDto?> UpdateMediaItemAsync(
        Guid id,
        UpdateMediaItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var existingItem = await _repository.GetByIdAsync(id, cancellationToken);
            if (existingItem == null)
            {
                _logger.LogWarning("Media item not found for update: {Id}", id);
                return null;
            }

            // Обновляем только переданные поля
            if (request.Title != null)
            {
                existingItem.Title = request.Title;
            }

            if (request.Description != null)
            {
                existingItem.Description = request.Description;
            }

            if (request.Type.HasValue)
            {
                existingItem.Type = request.Type.Value;
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

            if (request.EpisodeNumber.HasValue)
            {
                existingItem.EpisodeNumber = request.EpisodeNumber.Value;
            }

            if (request.Season != null)
            {
                existingItem.Season = request.Season;
            }

            if (request.Genre != null)
            {
                existingItem.Genre = request.Genre;
            }

            if (request.PosterUrl != null)
            {
                existingItem.PosterUrl = request.PosterUrl;
            }

            if (request.DurationMinutes.HasValue)
            {
                existingItem.DurationMinutes = request.DurationMinutes.Value;
            }

            if (request.IsNext.HasValue)
            {
                existingItem.IsNext = request.IsNext.Value;
            }

            existingItem.LastModified = DateTimeOffset.Now;

            var updatedItem = await _repository.UpdateAsync(existingItem, cancellationToken);
            _logger.LogInformation("Updated media item: {Id}", id);

            return updatedItem != null ? MapToDto(updatedItem) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating media item: {Id}", id);
            throw;
        }
    }

    public async Task<bool> DeleteMediaItemAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _repository.DeleteAsync(id, cancellationToken);
            if (result)
            {
                _logger.LogInformation("Deleted media item: {Id}", id);
            }
            else
            {
                _logger.LogWarning("Media item not found for deletion: {Id}", id);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting media item: {Id}", id);
            throw;
        }
    }

    public async Task<bool> MarkAsNextAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var existingItem = await _repository.GetByIdAsync(id, cancellationToken);
            if (existingItem == null)
            {
                _logger.LogWarning("Media item not found for marking as next: {Id}", id);
                return false;
            }

            // Сбрасываем флаг IsNext для всех элементов
            await _repository.ResetNextFlagsAsync(cancellationToken);

            // Устанавливаем флаг IsNext для выбранного элемента
            existingItem.IsNext = true;
            existingItem.LastModified = DateTimeOffset.Now;

            var updatedItem = await _repository.UpdateAsync(existingItem, cancellationToken);
            _logger.LogInformation(
                "Marked media item as next: {Id} - {Title}",
                id,
                existingItem.Title
            );

            return updatedItem != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking media item as next: {Id}", id);
            throw;
        }
    }

    public async Task<bool> ChangeStatusAsync(
        Guid id,
        MediaStatus status,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var existingItem = await _repository.GetByIdAsync(id, cancellationToken);
            if (existingItem == null)
            {
                _logger.LogWarning("Media item not found for status change: {Id}", id);
                return false;
            }

            existingItem.Status = status;
            existingItem.LastModified = DateTimeOffset.Now;

            var updatedItem = await _repository.UpdateAsync(existingItem, cancellationToken);
            _logger.LogInformation("Changed status of media item {Id} to {Status}", id, status);

            return updatedItem != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing status of media item: {Id}", id);
            throw;
        }
    }

    public async Task<bool> ChangePriorityAsync(
        Guid id,
        int priority,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var existingItem = await _repository.GetByIdAsync(id, cancellationToken);
            if (existingItem == null)
            {
                _logger.LogWarning("Media item not found for priority change: {Id}", id);
                return false;
            }

            existingItem.Priority = priority;
            existingItem.LastModified = DateTimeOffset.Now;

            var updatedItem = await _repository.UpdateAsync(existingItem, cancellationToken);
            _logger.LogInformation(
                "Changed priority of media item {Id} to {Priority}",
                id,
                priority
            );

            return updatedItem != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing priority of media item: {Id}", id);
            throw;
        }
    }

    public async Task<CinemaQueueStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var stats = new CinemaQueueStatistics();

            stats.PendingItems = await _repository.GetCountByStatusAsync(
                MediaStatus.Pending,
                cancellationToken
            );
            stats.InProgressItems = await _repository.GetCountByStatusAsync(
                MediaStatus.InProgress,
                cancellationToken
            );
            stats.CompletedItems = await _repository.GetCountByStatusAsync(
                MediaStatus.Completed,
                cancellationToken
            );
            stats.CancelledItems = await _repository.GetCountByStatusAsync(
                MediaStatus.Cancelled,
                cancellationToken
            );
            stats.PostponedItems = await _repository.GetCountByStatusAsync(
                MediaStatus.Postponed,
                cancellationToken
            );

            stats.MoviesCount = await _repository.GetCountByTypeAsync(
                MediaType.Movie,
                cancellationToken
            );
            stats.SeriesCount = await _repository.GetCountByTypeAsync(
                MediaType.Series,
                cancellationToken
            );
            stats.AnimeCount = await _repository.GetCountByTypeAsync(
                MediaType.Anime,
                cancellationToken
            );
            stats.DocumentaryCount = await _repository.GetCountByTypeAsync(
                MediaType.Documentary,
                cancellationToken
            );
            stats.SpecialCount = await _repository.GetCountByTypeAsync(
                MediaType.Special,
                cancellationToken
            );

            stats.TotalItems =
                stats.PendingItems
                + stats.InProgressItems
                + stats.CompletedItems
                + stats.CancelledItems
                + stats.PostponedItems;

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cinema queue statistics");
            throw;
        }
    }

    private static MediaItemDto MapToDto(MediaItem item)
    {
        return new MediaItemDto
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Type = item.Type,
            Status = item.Status,
            Priority = item.Priority,
            CreatedAt = item.CreatedAt,
            ScheduledFor = item.ScheduledFor,
            AddedBy = item.AddedBy,
            TwitchUserId = item.TwitchUserId,
            TwitchUsername = item.TwitchUsername,
            Notes = item.Notes,
            IsNext = item.IsNext,
            EpisodeNumber = item.EpisodeNumber,
            Season = item.Season,
            Genre = item.Genre,
            PosterUrl = item.PosterUrl,
            DurationMinutes = item.DurationMinutes,
            LastModified = item.LastModified,
        };
    }
}
