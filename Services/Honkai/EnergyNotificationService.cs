using MarchSeven;
using MarchSeven.Models.Core;
using MarchSeven.Models.Core.Cookie;
using MarchSeven.Models.HonkaiStarRail.Entitys;
using MARS.Server.Services.Honkai.Entitys;
using MARS.Server.Services.ServiceManager;

namespace MARS.Server.Services.Honkai;

public class EnergyNotificationService(
    ILogger<EnergyNotificationService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHttpClientFactory httpClientFactory,
    ITelegramBotClient telegramClient,
    ITwitchClient twitchClient
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "honkai-energy-notification";
    public override string DisplayName => "Honkai Energy Notification";
    public override string Description => "Уведомления об энергии в Honkai: Star Rail";
    public override bool IsServiceActive { get; set; } = true;

    private Timer? _energyCheckTimer;
    private readonly Dictionary<Guid, DateTime> _lastNotificationTime = new();
    private const int EnergyThreshold240 = 240;
    private const int EnergyThreshold300 = 300;
    private const int NotificationCooldownHours = 2; // Повторное уведомление не чаще чем через 2 часа

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Запуск сервиса уведомлений об энергии Honkai: Star Rail");

        // Проверяем энергию каждые 15 минут
        _energyCheckTimer = new Timer(TimeSpan.FromMinutes(15).TotalMilliseconds);
        _energyCheckTimer.Elapsed += async (sender, e) => await CheckEnergyForAllUsers();
        _energyCheckTimer.AutoReset = true;
        _energyCheckTimer.Start();

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        _energyCheckTimer?.Dispose();
        return base.StopAsync(cancellationToken);
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
            var client = MarchSevenClient.Create(
                new CookieV2()
                {
                    LTokenV2 = user.LTokenV2,
                    LtMidV2 = user.LtmidV2,
                    LtUidV2 = user.LtuidV2,
                },
                new ClientData() { HttpClient = httpClient, Language = "ru-RU" }
            );

            // Get user roles
            var gameRoles = await client.GetGameRoles();
            var starRailRole = gameRoles.Data?.List?.FirstOrDefault(r =>
                r.GameRegionName == "hkrpg_global"
            );

            if (starRailRole == null)
            {
                logger.LogDebug("Star Rail role not found for user {UserId}", user.Id);
                return;
            }

            var hsrUser = new StarRailUser(int.Parse(starRailRole.GameUid));
            logger.LogDebug($"UID: {hsrUser.Uid}");
            logger.LogDebug($"Server: {hsrUser.Server}");

            // Get daily note data
            var dailyNote = await client.StarRail.FetchDailyNoteAsync(hsrUser);

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
                await SendEnergyNotification(
                    user,
                    currentEnergy,
                    maxEnergy,
                    EnergyThreshold300,
                    hsrUser.Uid,
                    energyRecoveryTime
                );
            }
            else if (currentEnergy >= EnergyThreshold240)
            {
                await SendEnergyNotification(
                    user,
                    currentEnergy,
                    maxEnergy,
                    EnergyThreshold240,
                    hsrUser.Uid,
                    energyRecoveryTime
                );
            }

            logger.LogDebug(
                "Пользователь {UserId} имеет {CurrentEnergy}/{MaxEnergy} энергии (аккаунт {GameUid})",
                user.Id,
                currentEnergy,
                maxEnergy,
                hsrUser.Uid
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

    private async Task SendEnergyNotification(
        DailyAutoMarkupUser user,
        int currentEnergy,
        int maxEnergy,
        int threshold,
        int gameUid,
        TimeSpan recoveryTime
    )
    {
        // Проверяем кулдаун уведомлений
        var notificationKey = $"{user.Id}_{gameUid}_{threshold}";
        if (
            _lastNotificationTime.TryGetValue(Guid.Parse(notificationKey), out var lastNotification)
        )
        {
            if (DateTime.UtcNow - lastNotification < TimeSpan.FromHours(NotificationCooldownHours))
            {
                logger.LogDebug(
                    "Пропускаем уведомление для пользователя {UserId} из-за кулдауна",
                    user.Id
                );
                return;
            }
        }

        try
        {
            var message = GenerateEnergyNotificationMessage(
                currentEnergy,
                maxEnergy,
                threshold,
                recoveryTime
            );

            // Отправляем уведомление через Telegram, если есть TelegramId
            if (user.TelegramId.HasValue)
            {
                await SendTelegramNotification(user.TelegramId.Value, message, gameUid);
            }

            // Отправляем уведомление через Twitch, если есть TwitchId
            if (!string.IsNullOrEmpty(user.TwitchId))
            {
                await SendTwitchNotification(user.TwitchId, message, gameUid);
            }

            // Обновляем время последнего уведомления
            _lastNotificationTime[Guid.Parse(notificationKey)] = DateTime.UtcNow;

            logger.LogInformation(
                "Отправлено уведомление об энергии {Energy}/{MaxEnergy} для пользователя {UserId} (аккаунт {GameUid})",
                currentEnergy,
                maxEnergy,
                user.Id,
                gameUid
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при отправке уведомления об энергии для пользователя {UserId}",
                user.Id
            );
        }
    }

    private string GenerateEnergyNotificationMessage(
        int currentEnergy,
        int maxEnergy,
        int threshold,
        TimeSpan recoveryTime
    )
    {
        var thresholdText = threshold == EnergyThreshold300 ? "300" : "240";
        var recoveryTimeSpan = recoveryTime;

        var message = $"⚡ Внимание! Ваша энергия в Honkai: Star Rail достигла {thresholdText}!\n\n";
        message += $"🔋 Текущая энергия: {currentEnergy}/{maxEnergy}\n";

        if (recoveryTime.TotalSeconds > 0)
        {
            var hours = (int)recoveryTimeSpan.TotalHours;
            var minutes = recoveryTimeSpan.Minutes;
            message += $"⏰ Время до полного восстановления: {hours}ч {minutes}м\n";
        }

        message += "\n🎮 Не забудьте потратить энергию на фарм материалов!";

        return message;
    }

    private async Task SendTelegramNotification(long telegramId, string message, int gameUid)
    {
        try
        {
            await telegramClient.SendMessage(telegramId, message);
            logger.LogInformation(
                "Telegram уведомление отправлено для {TelegramId}: {Message}",
                telegramId,
                message
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при отправке Telegram уведомления для {TelegramId}",
                telegramId
            );
        }
    }

    private async Task SendTwitchNotification(string twitchId, string message, int gameUid)
    {
        try
        {
            await twitchClient.SendMessageToMainTwitchAsync(message);
            logger.LogInformation(
                "Twitch уведомление отправлено для {TwitchId}: {Message}",
                twitchId,
                message
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отправке Twitch уведомления для {TwitchId}", twitchId);
        }
    }

    public override Dictionary<string, object> GetServiceConfiguration()
    {
        return new Dictionary<string, object>
        {
            ["EnergyCheckInterval"] = "15 минут",
            ["EnergyThreshold240"] = EnergyThreshold240,
            ["EnergyThreshold300"] = EnergyThreshold300,
            ["NotificationCooldown"] = $"{NotificationCooldownHours} часа",
            ["SupportedPlatforms"] = "Telegram, Twitch",
        };
    }
}
