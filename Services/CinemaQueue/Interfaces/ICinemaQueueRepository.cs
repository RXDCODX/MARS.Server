using MARS.Server.Services.CinemaQueue.Entitys;
using MediaType = MARS.Server.Services.CinemaQueue.Entitys.MediaType;

namespace MARS.Server.Services.CinemaQueue.Interfaces;

public interface ICinemaQueueRepository
{
    /// <summary>
    /// Получить все элементы очереди
    /// </summary>
    Task<IEnumerable<MediaItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить элемент по ID
    /// </summary>
    Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить следующий элемент для просмотра
    /// </summary>
    Task<MediaItem?> GetNextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить элементы по статусу
    /// </summary>
    Task<IEnumerable<MediaItem>> GetByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить элементы по типу
    /// </summary>
    Task<IEnumerable<MediaItem>> GetByTypeAsync(
        MediaType type,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Создать новый элемент
    /// </summary>
    Task<MediaItem> CreateAsync(MediaItem mediaItem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновить элемент
    /// </summary>
    Task<MediaItem?> UpdateAsync(
        MediaItem mediaItem,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Удалить элемент
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сбросить флаг IsNext для всех элементов
    /// </summary>
    Task ResetNextFlagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить количество элементов по статусу
    /// </summary>
    Task<int> GetCountByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить количество элементов по типу
    /// </summary>
    Task<int> GetCountByTypeAsync(MediaType type, CancellationToken cancellationToken = default);
}
