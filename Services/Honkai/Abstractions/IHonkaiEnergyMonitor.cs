using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Abstractions;

/// <summary>
/// Интерфейс для мониторинга энергии пользователей Honkai
/// </summary>
public interface IHonkaiEnergyMonitor
{
    /// <summary>
    /// Проверяет уровень энергии пользователя и отправляет уведомления при необходимости
    /// </summary>
    /// <param name="user">Пользователь для проверки</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если проверка прошла успешно</returns>
    Task<bool> CheckEnergyAndNotifyAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет энергию для всех пользователей
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Количество успешно проверенных пользователей</returns>
    Task<int> CheckEnergyForAllUsersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Конфигурация для мониторинга энергии
/// </summary>
public class EnergyMonitoringConfiguration
{
    /// <summary>
    /// Пороговое значение энергии для первого уведомления (обычно 240)
    /// </summary>
    public int LowEnergyThreshold { get; set; } = 240;

    /// <summary>
    /// Пороговое значение энергии для критического уведомления (обычно 300)
    /// </summary>
    public int HighEnergyThreshold { get; set; } = 300;

    /// <summary>
    /// Интервал проверки энергии в минутах
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Кулдаун между уведомлениями в часах
    /// </summary>
    public int NotificationCooldownHours { get; set; } = 2;
}