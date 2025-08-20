using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Abstractions;

/// <summary>
/// Интерфейс для работы с пользователями Honkai в базе данных
/// </summary>
public interface IHonkaiUserRepository
{
    /// <summary>
    /// Получает всех пользователей, которым нужна ежедневная отметка
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Список пользователей, которым нужна отметка</returns>
    Task<List<DailyAutoMarkupUser>> GetUsersNeedingDailyMarkupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает всех пользователей для проверки энергии
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Список всех пользователей</returns>
    Task<List<DailyAutoMarkupUser>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает пользователей, у которых есть ошибки с отметками
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Список пользователей с ошибками</returns>
    Task<List<DailyAutoMarkupUser>> GetUsersWithMarkupErrorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет время последней отметки пользователя
    /// </summary>
    /// <param name="user">Пользователь для обновления</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если обновление прошло успешно</returns>
    Task<bool> UpdateLastMarkupTimeAsync(DailyAutoMarkupUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создает нового пользователя
    /// </summary>
    /// <param name="user">Данные пользователя</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Созданный пользователь</returns>
    Task<DailyAutoMarkupUser> CreateUserAsync(DailyAutoMarkupUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет существование пользователей в базе данных
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если пользователи существуют</returns>
    Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default);
}