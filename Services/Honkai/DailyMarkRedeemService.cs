using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai;

public class DailyMarkRedeemService(
    IOptions<HoyolabConfiguration> options,
    ILogger<DailyMarkRedeemService> logger,
    IHonkaiApiService honkaiApiService,
    IHonkaiNotificationService notificationService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHttpClientFactory httpClientFactory,
    IHostEnvironment environment
) : BackgroundService
{

    private readonly HoyolabConfiguration _configuration = options.Value;
    private Timer? _dailyTimer;
    private Timer? _errorNotificationTimer;
    private readonly TimeZoneInfo _honkaiTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        "China Standard Time" // UTC+8
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Запуск сервиса автоматических отметок Honkai: Star Rail");

        // Запускаем таймер для проверки каждые 30 минут
        _dailyTimer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
        _dailyTimer.Elapsed += async (sender, e) => await PerformDailyMarkRedeem();
        _dailyTimer.AutoReset = true;
        _dailyTimer.Start();

        await PerformDailyMarkRedeem();

        logger.LogInformation("Таймер запущен - проверка каждые 30 минут");

        // Планируем отправку уведомлений об ошибках за 2 часа до 00:00 по UTC+8
        ScheduleErrorNotifications();

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _dailyTimer?.Dispose();
        _errorNotificationTimer?.Dispose();
        await base.StopAsync(cancellationToken);
    }

    private void ScheduleErrorNotifications()
    {
        // Работаем с UTC временем, но используем логику UTC+8
        var now = DateTime.UtcNow;
        var targetTimeUtc = now.Date.AddHours(16); // 00:00 UTC+8 = 16:00 UTC (предыдущий день)
        var notificationTimeUtc = targetTimeUtc.AddHours(-2); // 22:00 UTC+8 = 14:00 UTC (предыдущий день)

        // Проверяем, находимся ли мы в промежутке между 22:00 и 00:00 по UTC+8
        // 22:00 UTC+8 = 14:00 UTC, 00:00 UTC+8 = 16:00 UTC
        var isInNotificationWindow =
            now.TimeOfDay >= TimeSpan.FromHours(14) && now.TimeOfDay < TimeSpan.FromHours(16);

        if (isInNotificationWindow)
        {
            logger.LogInformation(
                "Приложение запущено в промежутке между 22:00 и 00:00 по UTC+8. Немедленно отправляем уведомления об ошибках."
            );

            // Немедленно отправляем уведомления об ошибках
            Task.Run(async () => await SendErrorNotifications());

            // Планируем следующую отправку на завтра в 22:00 по UTC+8 (14:00 UTC)
            var tomorrowNotificationTime = now.Date.AddDays(1).AddHours(14);
            var delayUntilTomorrow = tomorrowNotificationTime - now;

            logger.LogInformation(
                "Следующие уведомления об ошибках запланированы на завтра в {NotificationTime} UTC+8 (через {Delay})",
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
            if (now >= notificationTimeUtc)
            {
                notificationTimeUtc = notificationTimeUtc.AddDays(1);
            }

            var delay = notificationTimeUtc - now;

            logger.LogInformation(
                "Уведомления об ошибках запланированы на {NotificationTime} UTC+8 (через {Delay})",
                TimeZoneInfo
                    .ConvertTime(notificationTimeUtc, _honkaiTimeZone)
                    .ToString("dd.MM.yyyy HH:mm"),
                delay
            );

            _errorNotificationTimer = new Timer(delay.TotalMilliseconds);
            _errorNotificationTimer.Elapsed += async (sender, e) => await SendErrorNotifications();
            _errorNotificationTimer.AutoReset = false; // Выполняем только один раз

            // Планируем следующий запуск на завтра
            var nextNotificationTime = notificationTimeUtc.AddDays(1);
            var nextDelay = nextNotificationTime - now;
            _errorNotificationTimer.Interval = nextDelay.TotalMilliseconds;
            _errorNotificationTimer.AutoReset = true;

            //_errorNotificationTimer.Start();
        }
    }

    /// <summary>
    /// Получает время последнего сброса цикла отметок в UTC+8
    /// </summary>
    private static DateTime GetLastCycleResetTime()
    {
        var now = DateTime.UtcNow;
        var todayResetTimeUtc = now.Date.AddHours(16); // 00:00 UTC+8 = 16:00 UTC (предыдущий день)

        // Если сейчас время до 16:00 UTC (00:00 UTC+8), то сброс был вчера
        return now.TimeOfDay < TimeSpan.FromHours(16)
            ? todayResetTimeUtc.AddDays(-1)
            : todayResetTimeUtc;
    }

    /// <summary>
    /// Проверяет, нужна ли пользователю отметка на основе времени последнего сброса цикла
    /// </summary>
    private bool NeedsMarkup(DailyAutoMarkupUser user)
    {
        var lastCycleReset = GetLastCycleResetTime();
        // user.LastAutoMarkup уже в UTC, поэтому конвертируем lastCycleReset в UTC для сравнения
        return user.LastAutoMarkup < lastCycleReset;
    }

    private async Task SendErrorNotifications()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var users = dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .AsEnumerable()
                .Where(NeedsMarkup)
                .ToList();

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
            var users = dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .AsEnumerable()
                .Where(NeedsMarkup)
                .ToList();

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
}
