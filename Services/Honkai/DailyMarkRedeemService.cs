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
    private System.Threading.Timer? _dailyTimer;
    private readonly TimeZoneInfo _ulyanovskTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        "Russian Standard Time"
    );

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            logger.LogInformation("Запуск сервиса автоматических отметок Honkai: Star Rail");

            // Запускаем таймер для ежедневных отметок (после 8 вечера по Ульяновскому времени)
            ScheduleDailyMarkRedeem();
        });

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        _dailyTimer?.Dispose();
        return base.StopAsync(cancellationToken);
    }

    private void ScheduleDailyMarkRedeem()
    {
        var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, _ulyanovskTimeZone);
        var targetTime = now.Date.AddHours(20); // 8 вечера

        // Если уже прошло 8 вечера, планируем на завтра
        if (now >= targetTime)
        {
            targetTime = targetTime.AddDays(1);
        }

        var delay = targetTime - now;

        logger.LogInformation(
            "Следующая автоматическая отметка запланирована на {TargetTime} (через {Delay})",
            targetTime.ToString("dd.MM.yyyy HH:mm"),
            delay
        );

        _dailyTimer = new System.Threading.Timer(
            PerformDailyMarkRedeem,
            null,
            delay,
            TimeSpan.FromDays(1)
        );
    }

    private async void PerformDailyMarkRedeem(object? state)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();

            logger.LogInformation("Начинаем автоматическую активацию ежедневных отметок");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var users = await dbContext
                .HonkaiMarkupUser.AsNoTracking()
                .Where(u => u.LastAutoMarkup < DateTime.UtcNow.Date)
                .ToListAsync();

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
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Ошибка при активации отметок для пользователя {UserId}",
                        user.Id
                    );
                }
            }

            logger.LogInformation(
                "Завершена автоматическая активация ежедневных отметок для {Count} пользователей",
                users.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выполнении автоматической активации отметок");
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
                new ClientData() { HttpClient = httpClient }
            );

            // Получаем информацию об аккаунте
            var accountInfo = await client.StarRail.FetchUserStatsAsync();

            if (accountInfo?.Data?.GameLists == null)
            {
                logger.LogWarning(
                    "Не удалось получить информацию об аккаунте для пользователя {UserId}",
                    user.Id
                );
                return;
            }

            try
            {
                var response = await client.StarRail.ClaimDailyRewardAsync();
                if (user.TelegramId != null)
                {
                    await telegramClient.SendMessage(
                        user.TelegramId,
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
                        user.TelegramId,
                        "Награда за отметки для HSR уже была активирована!"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при создании клиента MarchSeven для пользователя {UserId}",
                user.Id
            );
            throw;
        }
    }

    public override Dictionary<string, object> GetServiceConfiguration()
    {
        return new Dictionary<string, object>
        {
            ["TimeZone"] = _ulyanovskTimeZone.DisplayName,
            ["DailyMarkTime"] = "20:00",
            ["EnergyCheckInterval"] = "30 минут",
        };
    }
}
