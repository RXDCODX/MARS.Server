using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.ServiceManager;

namespace MARS.Server.Services.Honkai.ManagedServices;

/// <summary>
/// Управляемый сервис для автоматической активации ежедневных отметок в Honkai: Star Rail
/// </summary>
public class HonkaiDailyMarkRedeemManagedService : ManagedServiceBase
{
    private readonly IHonkaiDailyMarkupProcessor _markupProcessor;
    private readonly IHonkaiNotificationProvider _notificationProvider;
    private readonly IHonkaiUserRepository _userRepository;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly TimeZoneInfo _ulyanovskTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
    
    private Timer? _dailyTimer;
    private Timer? _errorNotificationTimer;

    /// <summary>
    /// Инициализирует новый экземпляр управляемого сервиса ежедневных отметок
    /// </summary>
    /// <param name="markupProcessor">Процессор ежедневных отметок</param>
    /// <param name="notificationProvider">Провайдер уведомлений</param>
    /// <param name="userRepository">Репозиторий пользователей</param>
    /// <param name="lifetime">Время жизни приложения</param>
    /// <param name="logger">Логгер для записи событий</param>
    public HonkaiDailyMarkRedeemManagedService(
        IHonkaiDailyMarkupProcessor markupProcessor,
        IHonkaiNotificationProvider notificationProvider,
        IHonkaiUserRepository userRepository,
        IHostApplicationLifetime lifetime,
        ILogger<HonkaiDailyMarkRedeemManagedService> logger)
        : base(logger)
    {
        _markupProcessor = markupProcessor;
        _notificationProvider = notificationProvider;
        _userRepository = userRepository;
        _lifetime = lifetime;
    }

    /// <summary>
    /// Название сервиса
    /// </summary>
    public override string ServiceName => "honkai-daily-mark-redeem";

    /// <summary>
    /// Отображаемое имя сервиса
    /// </summary>
    public override string DisplayName => "Honkai Daily Mark Redeem";

    /// <summary>
    /// Описание сервиса
    /// </summary>
    public override string Description => "Автоматическая активация ежедневных отметок в Honkai: Star Rail";

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
        _lifetime.ApplicationStarted.Register(() =>
        {
            Logger.LogInformation("Запуск сервиса автоматических отметок Honkai: Star Rail");

            // Запускаем таймер для проверки каждые 30 минут
            _dailyTimer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
            _dailyTimer.Elapsed += async (sender, e) => await PerformDailyMarkRedeem();
            _dailyTimer.AutoReset = true;
            _dailyTimer.Start();

            Task.Factory.StartNew(async () => await PerformDailyMarkRedeem(), cancellationToken);

            Logger.LogInformation("Таймер запущен - проверка каждые 30 минут");

            // Планируем отправку уведомлений об ошибках за 2 часа до 20:00
            ScheduleErrorNotifications();
        });

        return base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Останавливает сервис
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Task</returns>
    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        _dailyTimer?.Dispose();
        _errorNotificationTimer?.Dispose();
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
            ["TimeZone"] = _ulyanovskTimeZone.DisplayName,
            ["CheckInterval"] = "30 минут",
            ["ErrorNotificationTime"] = "18:00 (за 2 часа до 20:00)",
            ["Description"] = "Проверка каждые 30 минут + уведомления об ошибках за 2 часа до 20:00",
        };
    }

    /// <summary>
    /// Выполняет обработку ежедневных отметок
    /// </summary>
    /// <returns>Task</returns>
    private async Task PerformDailyMarkRedeem()
    {
        try
        {
            var result = await _markupProcessor.ProcessDailyMarkupsAsync();
            
            if (result.Errors.Count > 0)
            {
                Logger.LogWarning("Daily markup processing completed with {ErrorCount} errors", result.Errors.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during daily mark redeem process");
        }
    }

    /// <summary>
    /// Планирует отправку уведомлений об ошибках
    /// </summary>
    private void ScheduleErrorNotifications()
    {
        var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow.Date, _ulyanovskTimeZone);
        var targetTime = now.Date.AddHours(19); // 20:00 по ульяновскому времени
        var notificationTime = targetTime.AddHours(-2); // За 2 часа до 20:00

        // Проверяем, находимся ли мы в промежутке между 18:00 и 20:00
        var currentTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, _ulyanovskTimeZone);
        var isInNotificationWindow =
            currentTime.TimeOfDay >= TimeSpan.FromHours(17)
            && currentTime.TimeOfDay < TimeSpan.FromHours(19);

        if (isInNotificationWindow)
        {
            Logger.LogInformation(
                "Приложение запущено в промежутке между 18:00 и 20:00. Немедленно отправляем уведомления об ошибках."
            );

            // Немедленно отправляем уведомления об ошибках
            Task.Run(async () => await SendErrorNotifications());

            // Планируем следующую отправку на завтра в 18:00
            var tomorrowNotificationTime = currentTime.Date.AddDays(1).AddHours(17);
            var delayUntilTomorrow = tomorrowNotificationTime - currentTime;

            Logger.LogInformation(
                "Следующие уведомления об ошибках запланированы на завтра в {NotificationTime} (через {Delay})",
                tomorrowNotificationTime.ToString("dd.MM.yyyy HH:mm"),
                delayUntilTomorrow
            );

            _errorNotificationTimer = new Timer(delayUntilTomorrow.TotalMilliseconds);
            _errorNotificationTimer.Elapsed += async (sender, e) => await SendErrorNotifications();
            _errorNotificationTimer.AutoReset = true;
            _errorNotificationTimer.Start();
        }
        else
        {
            // Если время уведомлений уже прошло, планируем на завтра
            if (now >= notificationTime)
            {
                notificationTime = notificationTime.AddDays(1);
            }

            var delay = notificationTime - now;

            Logger.LogInformation(
                "Уведомления об ошибках запланированы на {NotificationTime} (через {Delay})",
                notificationTime.ToString("dd.MM.yyyy HH:mm"),
                delay
            );

            _errorNotificationTimer = new Timer(delay.TotalMilliseconds);
            _errorNotificationTimer.Elapsed += async (sender, e) => await SendErrorNotifications();
            _errorNotificationTimer.AutoReset = false; // Выполняем только один раз

            // Планируем следующий запуск на завтра
            var nextNotificationTime = notificationTime.AddDays(1);
            var nextDelay = nextNotificationTime - now;
            _errorNotificationTimer.Interval = nextDelay.TotalMilliseconds;
            _errorNotificationTimer.AutoReset = true;

            _errorNotificationTimer.Start();
        }
    }

    /// <summary>
    /// Отправляет уведомления об ошибках пользователям
    /// </summary>
    /// <returns>Task</returns>
    private async Task SendErrorNotifications()
    {
        try
        {
            var users = await _userRepository.GetUsersWithMarkupErrorsAsync();

            foreach (var user in users)
            {
                if (user.TelegramId != null)
                {
                    await _notificationProvider.SendMarkupFailureNotificationAsync(
                        user.TelegramId.Value,
                        user.Id
                    );
                }
            }

            Logger.LogInformation(
                "Отправлены уведомления об ошибках для {Count} пользователей",
                users.Count
            );

            // Планируем следующую отправку уведомлений на завтра
            // Не вызываем ScheduleErrorNotifications() здесь, так как таймер уже настроен
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка при отправке уведомлений об ошибках");
        }
    }
}