using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai;

public class EnergyNotificationService(
    ILogger<EnergyNotificationService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHttpClientFactory httpClientFactory,
    IHonkaiApiService honkaiApiService,
    IHonkaiNotificationService notificationService
) : BackgroundService
{

    private Timer? _energyCheckTimer;
    private const int EnergyThreshold240 = 240;
    private const int EnergyThreshold300 = 300;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Запуск сервиса уведомлений об энергии Honkai: Star Rail");

        // Проверяем энергию каждые 15 минут
        _energyCheckTimer = new Timer(TimeSpan.FromMinutes(15).TotalMilliseconds);
        _energyCheckTimer.Elapsed += async (sender, e) => await CheckEnergyForAllUsers();
        _energyCheckTimer.AutoReset = true;
        _energyCheckTimer.Start();

        await CheckEnergyForAllUsers();

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _energyCheckTimer?.Dispose();
        await base.StopAsync(cancellationToken);
    }

    private async Task CheckEnergyForAllUsers()
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var users = await dbContext.HonkaiMarkupUser.AsNoTracking().ToListAsync();

            foreach (var user in users)
            {
                try
                {
                    await CheckEnergyAndNotifyUser(user, httpClient);
                }
                catch (Exception ex)
                {
                    // В продакшене не логируем ошибки
                    logger.LogDebug(
                        "Ошибка при проверке энергии для пользователя {UserId}: {Error}",
                        user.Id,
                        ex.Message
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при проверке энергии пользователей");
        }
    }

    private async Task CheckEnergyAndNotifyUser(DailyAutoMarkupUser user, HttpClient httpClient)
    {
        try
        {
            // Get Star Rail user
            var starRailUser = await honkaiApiService.GetStarRailUserAsync(user, httpClient);
            if (starRailUser == null)
            {
                logger.LogDebug("Star Rail role not found for user {UserId}", user.Id);
                return;
            }

            // Get daily note data
            var dailyNote = await honkaiApiService.GetDailyNoteAsync(user, httpClient);

            if (dailyNote?.Data == null)
            {
                logger.LogDebug("Failed to get stamina data for user {UserId}", user.Id);
                return;
            }

            var currentEnergy = dailyNote.Data.CurrentStamina;
            var maxEnergy = dailyNote.Data.MaxStamina;
            var energyRecoveryTime = TimeSpan.FromSeconds(dailyNote.Data.StaminaRecoverTime);

            // Проверяем пороги энергии
            if (currentEnergy >= EnergyThreshold300)
            {
                await notificationService.SendEnergyNotificationAsync(
                    user,
                    currentEnergy,
                    maxEnergy,
                    EnergyThreshold300,
                    starRailUser.Uid,
                    energyRecoveryTime
                );
            }
            else if (currentEnergy >= EnergyThreshold240)
            {
                await notificationService.SendEnergyNotificationAsync(
                    user,
                    currentEnergy,
                    maxEnergy,
                    EnergyThreshold240,
                    starRailUser.Uid,
                    energyRecoveryTime
                );
            }

            logger.LogDebug(
                "Пользователь {UserId} имеет {CurrentEnergy}/{MaxEnergy} энергии (аккаунт {GameUid})",
                user.Id,
                currentEnergy,
                maxEnergy,
                starRailUser.Uid
            );
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                "Ошибка при получении информации об энергии для пользователя {UserId}: {Error}",
                user.Id,
                ex.Message
            );
        }
    }
}
