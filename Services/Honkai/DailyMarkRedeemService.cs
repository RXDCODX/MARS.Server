using MarchSeven;
using MarchSeven.Models.Core;
using MarchSeven.Models.Core.Cookie;
using MarchSeven.Util.Errors;
using MARS.Server.Services.Honkai.Entitys;
using MARS.Server.Services.ServiceManager;

namespace MARS.Server.Services.Honkai;

public class DailyMarkRedeemService(
    IOptions<HoyolabConfiguration> options,
    ILogger<DailyMarkRedeemService> logger,
    IHostApplicationLifetime lifetime,
    ITelegramBotClient telegramClient,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHttpClientFactory httpClientFactory
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
        var targetTime = now.Date.AddHours(20); // 20:00 по ульяновскому времени
        var notificationTime = targetTime.AddHours(-2); // За 2 часа до 20:00

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

        _errorNotificationTimer.Start();
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
                    await SendMarkupFailureNotification(user.TelegramId.Value, user.Id);
                }
            }

            logger.LogInformation(
                "Отправлены уведомления об ошибках для {Count} пользователей",
                users.Count
            );

            // Планируем следующую отправку уведомлений на завтра
            ScheduleErrorNotifications();
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
                    if (user.TelegramId != null)
                    {
                        await SendMarkupFailureNotification(user.TelegramId.Value, user.Id);
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

    private async Task SendMarkupFailureNotification(long telegramId, Guid userId)
    {
        try
        {
            var message =
                $"⚠️ **Внимание!**\n\n"
                + $"Не удалось поставить отметку в Honkai: Star Rail для пользователя {userId}.\n\n"
                + $"🕐 Время: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC\n"
                + $"📱 Попробуйте проверить настройки аккаунта или обратиться к администратору.\n\n"
                + $"⏰ Следующая попытка автоматической отметки будет через 30 минут.";

            await telegramClient.SendMessage(telegramId, message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отправке уведомления о неудачной отметке");
        }
    }

    private async Task RedeemDailyMarksForUser(DailyAutoMarkupUser user, HttpClient httpClient)
    {
        try
        {
            var client = MarchSevenClient.Create(
                new CookieV2()
                {
                    LTokenV2 = user.LTokenV2,
                    LtMidV2 = user.LtmidV2,
                    LtUidV2 = user.LtuidV2,
                },
                new ClientData() { HttpClient = httpClient, Language = "ru-RU" }
            );

            // Получаем информацию об аккаунте
            var accountInfo = await client.StarRail.FetchUserStatsAsync();

            if (accountInfo?.Data?.GameLists == null)
            {
                throw new Exception("Не удалось получить информацию об аккаунте");
            }

            try
            {
                var response = await client.StarRail.ClaimDailyRewardAsync();
                if (user.TelegramId != null)
                {
                    await telegramClient.SendMessage(
                        user.TelegramId.Value,
                        "Автоотметка была активирована! Награда за сегодня: "
                            + response.RewardName
                            + " в количестве "
                            + response.Amount
                            + " штук!"
                    );
                }
            }
            catch (DailyRewardAlreadyReceivedException)
            {
                if (user.TelegramId != null)
                {
                    await telegramClient.SendMessage(
                        user.TelegramId.Value,
                        "Награда за отметки для HSR уже была активирована!"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка при создании клиента MarchSeven: {ex.Message}");
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
