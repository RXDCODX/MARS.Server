using MARS.Server.Services.CinemaQueue.Entitys;
using MediaType = MARS.Server.Services.CinemaQueue.Entitys.MediaType;

namespace MARS.Server.Services.CinemaQueue.Interfaces;

public interface ICinemaQueueService
{
    /// <summary>
    /// Получить все элементы очереди
    /// </summary>
    Task<IEnumerable<MediaItemDto>> GetAllMediaItemsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить элемент по ID
    /// </summary>
    Task<MediaItemDto?> GetMediaItemByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить следующий элемент для просмотра
    /// </summary>
    Task<MediaItemDto?> GetNextMediaItemAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить элементы по статусу
    /// </summary>
    Task<IEnumerable<MediaItemDto>> GetMediaItemsByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить элементы по типу
    /// </summary>
    Task<IEnumerable<MediaItemDto>> GetMediaItemsByTypeAsync(
        MediaType type,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Создать новый элемент
    /// </summary>
    Task<MediaItemDto> CreateMediaItemAsync(
        CreateMediaItemRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Обновить элемент
    /// </summary>
    Task<MediaItemDto?> UpdateMediaItemAsync(
        Guid id,
        UpdateMediaItemRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Удалить элемент
    /// </summary>
    Task<bool> DeleteMediaItemAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отметить элемент как следующий для просмотра
    /// </summary>
    Task<bool> MarkAsNextAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменить статус элемента
    /// </summary>
    Task<bool> ChangeStatusAsync(
        Guid id,
        MediaStatus status,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Изменить приоритет элемента
    /// </summary>
    Task<bool> ChangePriorityAsync(
        Guid id,
        int priority,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить статистику очереди
    /// </summary>
    Task<CinemaQueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

public class CinemaQueueStatistics
{
    public int TotalItems { get; set; }
    public int PendingItems { get; set; }
    public int InProgressItems { get; set; }
    public int CompletedItems { get; set; }
    public int CancelledItems { get; set; }
    public int PostponedItems { get; set; }
    public int MoviesCount { get; set; }
    public int SeriesCount { get; set; }
    public int AnimeCount { get; set; }
    public int DocumentaryCount { get; set; }
    public int SpecialCount { get; set; }
}
