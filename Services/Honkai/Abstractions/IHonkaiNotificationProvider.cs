using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Abstractions;

/// <summary>
/// Интерфейс для отправки уведомлений пользователям Honkai
/// </summary>
public interface IHonkaiNotificationProvider
{
    /// <summary>
    /// Отправляет уведомление об ошибке получения ежедневной отметки
    /// </summary>
    /// <param name="telegramId">ID пользователя в Telegram</param>
    /// <param name="userId">ID пользователя в системе</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    Task<bool> SendMarkupFailureNotificationAsync(
        long telegramId, 
        Guid userId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправляет уведомление об успешном получении ежедневной отметки
    /// </summary>
    /// <param name="telegramId">ID пользователя в Telegram</param>
    /// <param name="rewardName">Название полученной награды</param>
    /// <param name="amount">Количество полученной награды</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    Task<bool> SendMarkupSuccessNotificationAsync(
        long telegramId, 
        string rewardName, 
        int amount, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправляет уведомление о том, что награда уже получена
    /// </summary>
    /// <param name="telegramId">ID пользователя в Telegram</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    Task<bool> SendMarkupAlreadyReceivedNotificationAsync(
        long telegramId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправляет уведомление об уровне энергии
    /// </summary>
    /// <param name="notification">Данные уведомления об энергии</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    Task<bool> SendEnergyNotificationAsync(
        EnergyNotificationData notification, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Данные для уведомления об энергии
/// </summary>
public class EnergyNotificationData
{
    /// <summary>
    /// Пользователь, которому отправляется уведомление
    /// </summary>
    public required DailyAutoMarkupUser User { get; set; }

    /// <summary>
    /// Текущий уровень энергии
    /// </summary>
    public int CurrentEnergy { get; set; }

    /// <summary>
    /// Максимальный уровень энергии
    /// </summary>
    public int MaxEnergy { get; set; }

    /// <summary>
    /// Пороговое значение энергии, при котором отправляется уведомление
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>
    /// UID игрового аккаунта
    /// </summary>
    public int GameUid { get; set; }

    /// <summary>
    /// Время до полного восстановления энергии
    /// </summary>
    public TimeSpan RecoveryTime { get; set; }
}