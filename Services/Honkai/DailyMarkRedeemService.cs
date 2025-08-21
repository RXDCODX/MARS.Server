using MARS.Server.Services.Honkai.Entitys;
using MARS.Server.Services.ServiceManager;

namespace MARS.Server.Services.Honkai;

public class DailyMarkRedeemService(
    IOptions<HoyolabConfiguration> options,
    ILogger<DailyMarkRedeemService> logger,
    IHostApplicationLifetime lifetime,
    IHonkaiApiService honkaiApiService,
    IHonkaiNotificationService notificationService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHttpClientFactory httpClientFactory,
    IHostEnvironment environment
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "honkai-daily-mark-redeem";
    public override string DisplayName => "Honkai Daily Mark Redeem";
    public override string Description =>
        "Автоматическая активация ежедневных отметок в Honkai: Star Rail";
    public override bool IsServiceActive { get; set; } = true;

    private readonly HoyolabConfiguration _configuration = options.Value;
    private Timer? _dailyTimer;
    private Timer? _errorNotificationTimer;
    private readonly TimeZoneInfo _ulyanovskTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        "Russian Standard Time"
    );

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            logger.LogInformation("Запуск сервиса автоматических отметок Honkai: Star Rail");

            // Запускаем таймер для проверки каждые 30 минут
            _dailyTimer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
            _dailyTimer.Elapsed += async (sender, e) => await PerformDailyMarkRedeem();
            _dailyTimer.AutoReset = true;
            _dailyTimer.Start();

            Task.Factory.StartNew(async () => await PerformDailyMarkRedeem(), cancellationToken);

            logger.LogInformation("Таймер запущен - проверка каждые 30 минут");

            // Планируем отправку уведомлений об ошибках за 2 часа до 20:00
            ScheduleErrorNotifications();
        });

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        _dailyTimer?.Dispose();
        _errorNotificationTimer?.Dispose();
        return base.StopAsync(cancellationToken);
    }

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
            logger.LogInformation(
                "Приложение запущено в промежутке между 18:00 и 20:00. Немедленно отправляем уведомления об ошибках."
            );

            // Немедленно отправляем уведомления об ошибках
            Task.Run(async () => await SendErrorNotifications());

            // Планируем следующую отправку на завтра в 18:00
            var tomorrowNotificationTime = currentTime.Date.AddDays(1).AddHours(17);
            var delayUntilTomorrow = tomorrowNotificationTime - currentTime;

            logger.LogInformation(
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

            logger.LogInformation(
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

            //_errorNotificationTimer.Start();
        }
    }

    private async Task SendErrorNotifications()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var users = await dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .Where(u => u.LastAutoMarkup < DateTime.UtcNow.Date)
                .ToListAsync();

            foreach (var user in users)
            {
                if (user.TelegramId != null)
                {
                    await notificationService.SendMarkupFailureNotificationAsync(
                        user.TelegramId.Value,
                        user.Id
                    );
                }
            }

            logger.LogInformation(
                "Отправлены уведомления об ошибках для {Count} пользователей",
                users.Count
            );

            // Планируем следующую отправку уведомлений на завтра
            // Не вызываем ScheduleErrorNotifications() здесь, так как таймер уже настроен
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отправке уведомлений об ошибках");
        }
    }

    private async Task PerformDailyMarkRedeem()
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();

            logger.LogInformation("Начинаем проверку и активацию ежедневных отметок");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var users = await dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .Where(u => u.LastAutoMarkup < DateTime.UtcNow.Date)
                .ToListAsync();

            if (users.Count == 0)
            {
                logger.LogDebug("Нет пользователей, требующих отметки");
                return;
            }

            logger.LogInformation("Найдено {Count} пользователей для отметки", users.Count);

            foreach (var user in users)
            {
                try
                {
                    await RedeemDailyMarksForUser(user, httpClient);

                    // Обновляем время последней отметки
                    user.LastAutoMarkup = DateTime.UtcNow;
                    dbContext.HonkaiMarkupUser.Update(user);
                    await dbContext.SaveChangesAsync();

                    logger.LogInformation(
                        "Успешно активированы отметки для пользователя {UserId}",
                        user.Id
                    );
                }
                catch (Exception)
                {
                    // В продакшене не логируем ошибки, только отправляем уведомление
                    if (user.TelegramId != null && environment.IsProduction())
                    {
                        //await notificationService.SendMarkupFailureNotificationAsync(
                        //    user.TelegramId.Value,
                        //    user.Id
                        //);
                    }
                }
            }

            logger.LogInformation(
                "Завершена проверка ежедневных отметок для {Count} пользователей",
                users.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выполнении проверки ежедневных отметок");
        }
    }

    private async Task RedeemDailyMarksForUser(DailyAutoMarkupUser user, HttpClient httpClient)
    {
        try
        {
            var (success, rewardName, amount) = await honkaiApiService.ClaimDailyRewardAsync(
                user,
                httpClient
            );
            if (success && user.TelegramId != null)
            {
                // Отправляем уведомление об успехе с информацией о награде
                await notificationService.SendMarkupSuccessNotificationAsync(
                    user.TelegramId.Value,
                    rewardName ?? "награда",
                    amount ?? 1
                );
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка при работе с Honkai API: {ex.Message}");
        }
    }

    public override Dictionary<string, object> GetServiceConfiguration()
    {
        return new Dictionary<string, object>
        {
            ["TimeZone"] = _ulyanovskTimeZone.DisplayName,
            ["CheckInterval"] = "30 минут",
            ["ErrorNotificationTime"] = "18:00 (за 2 часа до 20:00)",
            ["Description"] =
                "Проверка каждые 30 минут + уведомления об ошибках за 2 часа до 20:00",
        };
    }
}
