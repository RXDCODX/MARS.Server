using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Services;

/// <summary>
/// Сервис для мониторинга уровня энергии пользователей Honkai: Star Rail
/// </summary>
public class HonkaiEnergyMonitor : IHonkaiEnergyMonitor
{
    private readonly IHonkaiGameApiClient _gameApiClient;
    private readonly IHonkaiNotificationProvider _notificationProvider;
    private readonly IHonkaiUserRepository _userRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HonkaiEnergyMonitor> _logger;
    private readonly EnergyMonitoringConfiguration _configuration;

    /// <summary>
    /// Инициализирует новый экземпляр монитора энергии Honkai
    /// </summary>
    /// <param name="gameApiClient">Клиент для взаимодействия с API игры</param>
    /// <param name="notificationProvider">Провайдер уведомлений</param>
    /// <param name="userRepository">Репозиторий пользователей</param>
    /// <param name="httpClientFactory">Фабрика HTTP клиентов</param>
    /// <param name="logger">Логгер для записи событий</param>
    /// <param name="configuration">Конфигурация мониторинга энергии</param>
    public HonkaiEnergyMonitor(
        IHonkaiGameApiClient gameApiClient,
        IHonkaiNotificationProvider notificationProvider,
        IHonkaiUserRepository userRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<HonkaiEnergyMonitor> logger,
        EnergyMonitoringConfiguration configuration)
    {
        _gameApiClient = gameApiClient;
        _notificationProvider = notificationProvider;
        _userRepository = userRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Проверяет уровень энергии пользователя и отправляет уведомления при необходимости
    /// </summary>
    /// <param name="user">Пользователь для проверки</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если проверка прошла успешно</returns>
    public async Task<bool> CheckEnergyAndNotifyAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Получаем информацию о пользователе Star Rail
            var starRailUser = await _gameApiClient.GetStarRailUserAsync(user, httpClient, cancellationToken);
            if (starRailUser == null)
            {
                _logger.LogDebug("Star Rail role not found for user {UserId}", user.Id);
                return false;
            }

            // Получаем данные об энергии
            var dailyNote = await _gameApiClient.GetDailyNoteAsync(user, httpClient, cancellationToken);
            if (dailyNote?.Data == null)
            {
                _logger.LogDebug("Failed to get stamina data for user {UserId}", user.Id);
                return false;
            }

            var currentEnergy = dailyNote.Data.CurrentStamina;
            var maxEnergy = dailyNote.Data.MaxStamina;
            var energyRecoveryTime = TimeSpan.FromSeconds(dailyNote.Data.StaminaRecoverTime);

            // Проверяем пороги энергии и отправляем уведомления
            if (currentEnergy >= _configuration.HighEnergyThreshold)
            {
                await SendEnergyNotification(user, currentEnergy, maxEnergy, 
                    _configuration.HighEnergyThreshold, starRailUser.Uid, energyRecoveryTime, cancellationToken);
            }
            else if (currentEnergy >= _configuration.LowEnergyThreshold)
            {
                await SendEnergyNotification(user, currentEnergy, maxEnergy, 
                    _configuration.LowEnergyThreshold, starRailUser.Uid, energyRecoveryTime, cancellationToken);
            }

            _logger.LogDebug(
                "Пользователь {UserId} имеет {CurrentEnergy}/{MaxEnergy} энергии (аккаунт {GameUid})",
                user.Id,
                currentEnergy,
                maxEnergy,
                starRailUser.Uid
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                "Ошибка при получении информации об энергии для пользователя {UserId}: {Error}",
                user.Id,
                ex.Message
            );
            return false;
        }
    }

    /// <summary>
    /// Проверяет энергию для всех пользователей
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Количество успешно проверенных пользователей</returns>
    public async Task<int> CheckEnergyForAllUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var users = await _userRepository.GetAllUsersAsync(cancellationToken);
            var successCount = 0;

            foreach (var user in users)
            {
                try
                {
                    var success = await CheckEnergyAndNotifyAsync(user, httpClient, cancellationToken);
                    if (success) successCount++;
                }
                catch (Exception ex)
                {
                    // В продакшене не логируем ошибки
                    _logger.LogDebug(
                        "Ошибка при проверке энергии для пользователя {UserId}: {Error}",
                        user.Id,
                        ex.Message
                    );
                }
            }

            _logger.LogInformation("Energy check completed for {SuccessCount}/{TotalCount} users", 
                successCount, users.Count);

            return successCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking energy for all users");
            return 0;
        }
    }

    /// <summary>
    /// Отправляет уведомление об энергии
    /// </summary>
    /// <param name="user">Пользователь</param>
    /// <param name="currentEnergy">Текущая энергия</param>
    /// <param name="maxEnergy">Максимальная энергия</param>
    /// <param name="threshold">Пороговое значение</param>
    /// <param name="gameUid">UID игрового аккаунта</param>
    /// <param name="recoveryTime">Время восстановления</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Task</returns>
    private async Task SendEnergyNotification(
        DailyAutoMarkupUser user,
        int currentEnergy,
        int maxEnergy,
        int threshold,
        int gameUid,
        TimeSpan recoveryTime,
        CancellationToken cancellationToken = default)
    {
        var notificationData = new EnergyNotificationData
        {
            User = user,
            CurrentEnergy = currentEnergy,
            MaxEnergy = maxEnergy,
            Threshold = threshold,
            GameUid = gameUid,
            RecoveryTime = recoveryTime
        };

        await _notificationProvider.SendEnergyNotificationAsync(notificationData, cancellationToken);
    }
}