using MARS.Server.Services.CinemaQueue.Entitys;

namespace MARS.Server.Services.CinemaQueue.Interfaces;

public interface ICinemaQueueService
{
    /// <summary>
    /// Получить все элементы очереди
    /// </summary>
    Task<IEnumerable<CinemaMediaItemDto>> GetAllMediaItemsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить элемент по ID
    /// </summary>
    Task<CinemaMediaItemDto?> GetMediaItemByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить следующий элемент для просмотра
    /// </summary>
    Task<CinemaMediaItemDto?> GetNextMediaItemAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить элементы по статусу
    /// </summary>
    Task<IEnumerable<CinemaMediaItemDto>> GetMediaItemsByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Создать новый элемент
    /// </summary>
    Task<CinemaMediaItemDto> CreateMediaItemAsync(
        CreateMediaItemRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Обновить элемент
    /// </summary>
    Task<CinemaMediaItemDto?> UpdateMediaItemAsync(
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

