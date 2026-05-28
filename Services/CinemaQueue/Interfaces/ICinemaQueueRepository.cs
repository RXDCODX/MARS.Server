using System.Collections.Generic;
using System.Threading;
using MARS.Server.Services.CinemaQueue.Entitys;

namespace MARS.Server.Services.CinemaQueue.Interfaces;

public interface ICinemaQueueRepository
{
    /// <summary>
    /// Получить все элементы очереди
    /// </summary>
    Task<IEnumerable<CinemaMediaItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить элемент по ID
    /// </summary>
    Task<CinemaMediaItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить следующий элемент для просмотра
    /// </summary>
    Task<CinemaMediaItem?> GetNextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить элементы по статусу
    /// </summary>
    Task<IEnumerable<CinemaMediaItem>> GetByStatusAsync(
        MediaStatus status,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Создать новый элемент
    /// </summary>
    Task<CinemaMediaItem> CreateAsync(
        CinemaMediaItem cinemaMediaItem,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Обновить элемент
    /// </summary>
    Task<CinemaMediaItem?> UpdateAsync(
        CinemaMediaItem cinemaMediaItem,
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
}
