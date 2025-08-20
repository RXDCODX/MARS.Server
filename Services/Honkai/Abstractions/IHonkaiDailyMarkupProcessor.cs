using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Abstractions;

/// <summary>
/// Интерфейс для обработки ежедневных отметок Honkai
/// </summary>
public interface IHonkaiDailyMarkupProcessor
{
    /// <summary>
    /// Обрабатывает ежедневные отметки для всех пользователей
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат обработки отметок</returns>
    Task<DailyMarkupProcessingResult> ProcessDailyMarkupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Обрабатывает ежедневные отметки для конкретного пользователя
    /// </summary>
    /// <param name="user">Пользователь для обработки</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат обработки отметки для пользователя</returns>
    Task<UserMarkupResult> ProcessUserMarkupAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат обработки ежедневных отметок
/// </summary>
public class DailyMarkupProcessingResult
{
    /// <summary>
    /// Общее количество пользователей, требующих отметки
    /// </summary>
    public int TotalUsersToProcess { get; set; }

    /// <summary>
    /// Количество успешно обработанных пользователей
    /// </summary>
    public int SuccessfullyProcessed { get; set; }

    /// <summary>
    /// Количество пользователей с ошибками
    /// </summary>
    public int FailedToProcess { get; set; }

    /// <summary>
    /// Время начала обработки
    /// </summary>
    public DateTime ProcessingStartTime { get; set; }

    /// <summary>
    /// Время окончания обработки
    /// </summary>
    public DateTime ProcessingEndTime { get; set; }

    /// <summary>
    /// Список ошибок, возникших при обработке
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Результат обработки отметки для конкретного пользователя
/// </summary>
public class UserMarkupResult
{
    /// <summary>
    /// Успешно ли обработана отметка
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Пользователь, для которого обрабатывалась отметка
    /// </summary>
    public required DailyAutoMarkupUser User { get; set; }

    /// <summary>
    /// Результат получения награды
    /// </summary>
    public HonkaiRewardResult? RewardResult { get; set; }

    /// <summary>
    /// Сообщение об ошибке, если обработка не удалась
    /// </summary>
    public string? ErrorMessage { get; set; }
}