using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.ServiceManager;

namespace MARS.Server.Services.Honkai.ManagedServices;

/// <summary>
/// Управляемый сервис для отправки уведомлений об энергии в Honkai: Star Rail
/// </summary>
public class HonkaiEnergyNotificationManagedService : ManagedServiceBase
{
    private readonly IHonkaiEnergyMonitor _energyMonitor;
    private readonly EnergyMonitoringConfiguration _configuration;
    
    private Timer? _energyCheckTimer;

    /// <summary>
    /// Инициализирует новый экземпляр управляемого сервиса уведомлений об энергии
    /// </summary>
    /// <param name="energyMonitor">Монитор энергии</param>
    /// <param name="configuration">Конфигурация мониторинга энергии</param>
    /// <param name="logger">Логгер для записи событий</param>
    public HonkaiEnergyNotificationManagedService(
        IHonkaiEnergyMonitor energyMonitor,
        EnergyMonitoringConfiguration configuration,
        ILogger<HonkaiEnergyNotificationManagedService> logger)
        : base(logger)
    {
        _energyMonitor = energyMonitor;
        _configuration = configuration;
    }

    /// <summary>
    /// Название сервиса
    /// </summary>
    public override string ServiceName => "honkai-energy-notification";

    /// <summary>
    /// Отображаемое имя сервиса
    /// </summary>
    public override string DisplayName => "Honkai Energy Notification";

    /// <summary>
    /// Описание сервиса
    /// </summary>
    public override string Description => "Уведомления об энергии в Honkai: Star Rail";

    /// <summary>
    /// Активен ли сервис
    /// </summary>
    public override bool IsServiceActive { get; set; } = true;

    /// <summary>
    /// Запускает сервис
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Task</returns>
    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Запуск сервиса уведомлений об энергии Honkai: Star Rail");

        // Проверяем энергию с заданным интервалом
        _energyCheckTimer = new Timer(TimeSpan.FromMinutes(_configuration.CheckIntervalMinutes).TotalMilliseconds);
        _energyCheckTimer.Elapsed += async (sender, e) => await CheckEnergyForAllUsers();
        _energyCheckTimer.AutoReset = true;
        _energyCheckTimer.Start();

        return base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Останавливает сервис
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Task</returns>
    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        _energyCheckTimer?.Dispose();
        return base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Получает конфигурацию сервиса
    /// </summary>
    /// <returns>Словарь с конфигурацией сервиса</returns>
    public override Dictionary<string, object> GetServiceConfiguration()
    {
        return new Dictionary<string, object>
        {
            ["EnergyCheckInterval"] = $"{_configuration.CheckIntervalMinutes} минут",
            ["LowEnergyThreshold"] = _configuration.LowEnergyThreshold,
            ["HighEnergyThreshold"] = _configuration.HighEnergyThreshold,
            ["NotificationCooldown"] = $"{_configuration.NotificationCooldownHours} часов",
            ["SupportedPlatforms"] = "Telegram, Twitch",
        };
    }

    /// <summary>
    /// Проверяет энергию для всех пользователей
    /// </summary>
    /// <returns>Task</returns>
    private async Task CheckEnergyForAllUsers()
    {
        try
        {
            var checkedCount = await _energyMonitor.CheckEnergyForAllUsersAsync();
            
            if (checkedCount > 0)
            {
                UpdateActivity();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during energy check process");
        }
    }
}