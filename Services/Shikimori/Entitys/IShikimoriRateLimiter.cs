namespace MARS.Server.Services.Shikimori.Entitys;

/// <summary>
/// Интерфейс для ограничения частоты запросов к API
/// </summary>
public interface IShikimoriRateLimiter
{
    /// <summary>
    /// Проверяет, можно ли выполнить запрос, и если да - резервирует слот
    /// </summary>
    /// <returns>True, если запрос можно выполнить, иначе false</returns>
    Task<bool> TryAcquireAsync();

    /// <summary>
    /// Ожидает доступности слота для выполнения запроса
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Task, который завершится, когда слот станет доступен</returns>
    Task WaitForSlotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает информацию о текущем состоянии лимитера
    /// </summary>
    /// <returns>Информация о доступных слотах</returns>
    RateLimiterInfo GetInfo();
}
