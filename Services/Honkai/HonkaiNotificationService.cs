using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai;

public interface IHonkaiNotificationService
{
    Task SendMarkupFailureNotificationAsync(long telegramId, Guid userId);
    Task SendMarkupSuccessNotificationAsync(long telegramId, string rewardName, int amount);
    Task SendMarkupAlreadyReceivedNotificationAsync(long telegramId);
    Task SendEnergyNotificationAsync(
        DailyAutoMarkupUser user,
        int currentEnergy,
        int maxEnergy,
        int threshold,
        int gameUid,
        TimeSpan recoveryTime
    );
}

public class HonkaiNotificationService(
    ITelegramBotClient telegramClient,
    ITwitchClient twitchClient,
    ILogger<HonkaiNotificationService> logger
) : IHonkaiNotificationService
{
    private readonly Dictionary<Guid, DateTime> _lastNotificationTime = new();
    private const int NotificationCooldownHours = 2;

    public async Task SendMarkupFailureNotificationAsync(long telegramId, Guid userId)
    {
        try
        {
            var message = GenerateMarkupFailureMessage(userId);
            await telegramClient.SendMessage(telegramId, message);

            logger.LogInformation(
                "Markup failure notification sent to Telegram {TelegramId} for user {UserId}",
                telegramId,
                userId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error sending markup failure notification to Telegram {TelegramId}",
                telegramId
            );
        }
    }

    public async Task SendMarkupSuccessNotificationAsync(
        long telegramId,
        string rewardName,
        int amount
    )
    {
        try
        {
            var message = GenerateMarkupSuccessMessage(rewardName, amount);
            await telegramClient.SendMessage(telegramId, message);

            logger.LogInformation(
                "Markup success notification sent to Telegram {TelegramId}",
                telegramId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error sending markup success notification to Telegram {TelegramId}",
                telegramId
            );
        }
    }

    public async Task SendMarkupAlreadyReceivedNotificationAsync(long telegramId)
    {
        try
        {
            var message = GenerateMarkupAlreadyReceivedMessage();
            await telegramClient.SendMessage(telegramId, message);

            logger.LogInformation(
                "Markup already received notification sent to Telegram {TelegramId}",
                telegramId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error sending markup already received notification to Telegram {TelegramId}",
                telegramId
            );
        }
    }

    public async Task SendEnergyNotificationAsync(
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
                    "Skipping energy notification for user {UserId} due to cooldown",
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
                "Energy notification sent for user {UserId} (account {GameUid}): {CurrentEnergy}/{MaxEnergy}",
                user.Id,
                gameUid,
                currentEnergy,
                maxEnergy
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending energy notification for user {UserId}", user.Id);
        }
    }

    private string GenerateMarkupFailureMessage(Guid userId)
    {
        return $"⚠️ **Внимание!**\n\n"
            + $"Не удалось поставить отметку в Honkai: Star Rail для пользователя {userId}.\n\n"
            + $"🕐 Время: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC\n"
            + $"📱 Попробуйте проверить настройки аккаунта или обратиться к администратору.\n\n"
            + $"⏰ Следующая попытка автоматической отметки будет через 30 минут."
            + "Вот ссылка для ручной активации - https://act.hoyolab.com/bbs/event/signin/hkrpg/index.html?act_id=e202303301540311";
    }

    private string GenerateMarkupSuccessMessage(string rewardName, int amount)
    {
        return $"Автоотметка была активирована! Награда за сегодня: {rewardName} в количестве {amount} штук!";
    }

    private string GenerateMarkupAlreadyReceivedMessage()
    {
        return "Награда за отметки для HSR уже была активирована!";
    }

    private string GenerateEnergyNotificationMessage(
        int currentEnergy,
        int maxEnergy,
        int threshold,
        TimeSpan recoveryTime
    )
    {
        var thresholdText = threshold == 300 ? "300" : "240";
        var message = $"⚡ Внимание! Ваша энергия в Honkai: Star Rail достигла {thresholdText}!\n\n";
        message += $"🔋 Текущая энергия: {currentEnergy}/{maxEnergy}\n";

        if (recoveryTime.TotalSeconds > 0)
        {
            var hours = (int)recoveryTime.TotalHours;
            var minutes = recoveryTime.Minutes;
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
                "Telegram notification sent for {TelegramId}: {Message}",
                telegramId,
                message
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Telegram notification for {TelegramId}", telegramId);
        }
    }

    private async Task SendTwitchNotification(string twitchId, string message, int gameUid)
    {
        try
        {
            await twitchClient.SendMessageToMainTwitchAsync(message);
            logger.LogInformation(
                "Twitch notification sent for {TwitchId}: {Message}",
                twitchId,
                message
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Twitch notification for {TwitchId}", twitchId);
        }
    }
}
