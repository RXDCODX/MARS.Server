using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Abstractions;

/// <summary>
/// Интерфейс для работы с ежедневными наградами Honkai: Star Rail
/// </summary>
public interface IHonkaiRewardService
{
    /// <summary>
    /// Получает ежедневную награду для пользователя
    /// </summary>
    /// <param name="user">Пользователь для получения награды</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат получения награды с информацией о награде</returns>
    Task<HonkaiRewardResult> ClaimDailyRewardAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат получения ежедневной награды
/// </summary>
public class HonkaiRewardResult
{
    /// <summary>
    /// Успешно ли получена награда
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Название полученной награды
    /// </summary>
    public string? RewardName { get; set; }

    /// <summary>
    /// Количество полученной награды
    /// </summary>
    public int? Amount { get; set; }

    /// <summary>
    /// Сообщение об ошибке, если награда не получена
    /// </summary>
    public string? ErrorMessage { get; set; }
}