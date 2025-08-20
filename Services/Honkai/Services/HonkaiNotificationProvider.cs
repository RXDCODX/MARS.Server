using MarchSeven;
using MarchSeven.Models.Core;
using MarchSeven.Models.Core.Cookie;
using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Services;

/// <summary>
/// Провайдер уведомлений для пользователей Honkai через различные платформы
/// </summary>
public class HonkaiNotificationProvider : IHonkaiNotificationProvider
{
    private readonly ITelegramBotClient _telegramClient;
    private readonly ITwitchClient _twitchClient;
    private readonly ILogger<HonkaiNotificationProvider> _logger;
    private readonly Dictionary<Guid, DateTime> _lastNotificationTime = new();
    private const int NotificationCooldownHours = 2;

    /// <summary>
    /// Инициализирует новый экземпляр провайдера уведомлений Honkai
    /// </summary>
    /// <param name="telegramClient">Клиент для отправки уведомлений в Telegram</param>
    /// <param name="twitchClient">Клиент для отправки уведомлений в Twitch</param>
    /// <param name="logger">Логгер для записи событий</param>
    public HonkaiNotificationProvider(
        ITelegramBotClient telegramClient,
        ITwitchClient twitchClient,
        ILogger<HonkaiNotificationProvider> logger)
    {
        _telegramClient = telegramClient;
        _twitchClient = twitchClient;
        _logger = logger;
    }

    /// <summary>
    /// Отправляет уведомление об ошибке получения ежедневной отметки
    /// </summary>
    /// <param name="telegramId">ID пользователя в Telegram</param>
    /// <param name="userId">ID пользователя в системе</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    public async Task<bool> SendMarkupFailureNotificationAsync(
        long telegramId, 
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = GenerateMarkupFailureMessage(userId);
            await _telegramClient.SendMessage(telegramId, message, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Markup failure notification sent to Telegram {TelegramId} for user {UserId}",
                telegramId,
                userId
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending markup failure notification to Telegram {TelegramId}",
                telegramId
            );
            return false;
        }
    }

    /// <summary>
    /// Отправляет уведомление об успешном получении ежедневной отметки
    /// </summary>
    /// <param name="telegramId">ID пользователя в Telegram</param>
    /// <param name="rewardName">Название полученной награды</param>
    /// <param name="amount">Количество полученной награды</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    public async Task<bool> SendMarkupSuccessNotificationAsync(
        long telegramId, 
        string rewardName, 
        int amount, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = GenerateMarkupSuccessMessage(rewardName, amount);
            await _telegramClient.SendMessage(telegramId, message, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Markup success notification sent to Telegram {TelegramId}",
                telegramId
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending markup success notification to Telegram {TelegramId}",
                telegramId
            );
            return false;
        }
    }

    /// <summary>
    /// Отправляет уведомление о том, что награда уже получена
    /// </summary>
    /// <param name="telegramId">ID пользователя в Telegram</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    public async Task<bool> SendMarkupAlreadyReceivedNotificationAsync(
        long telegramId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = GenerateMarkupAlreadyReceivedMessage();
            await _telegramClient.SendMessage(telegramId, message, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Markup already received notification sent to Telegram {TelegramId}",
                telegramId
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending markup already received notification to Telegram {TelegramId}",
                telegramId
            );
            return false;
        }
    }

    /// <summary>
    /// Отправляет уведомление об уровне энергии
    /// </summary>
    /// <param name="notification">Данные уведомления об энергии</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    public async Task<bool> SendEnergyNotificationAsync(
        EnergyNotificationData notification, 
        CancellationToken cancellationToken = default)
    {
        // Проверяем кулдаун уведомлений
        var notificationKey = $"{notification.User.Id}_{notification.GameUid}_{notification.Threshold}";
        if (_lastNotificationTime.TryGetValue(Guid.Parse(notificationKey), out var lastNotification))
        {
            if (DateTime.UtcNow - lastNotification < TimeSpan.FromHours(NotificationCooldownHours))
            {
                _logger.LogDebug(
                    "Skipping energy notification for user {UserId} due to cooldown",
                    notification.User.Id
                );
                return false;
            }
        }

        try
        {
            var message = GenerateEnergyNotificationMessage(
                notification.CurrentEnergy,
                notification.MaxEnergy,
                notification.Threshold,
                notification.RecoveryTime
            );

            var success = false;

            // Отправляем уведомление через Telegram, если есть TelegramId
            if (notification.User.TelegramId.HasValue)
            {
                success |= await SendTelegramNotification(
                    notification.User.TelegramId.Value, 
                    message, 
                    notification.GameUid, 
                    cancellationToken);
            }

            // Отправляем уведомление через Twitch, если есть TwitchId
            if (!string.IsNullOrEmpty(notification.User.TwitchId))
            {
                success |= await SendTwitchNotification(
                    notification.User.TwitchId, 
                    message, 
                    notification.GameUid, 
                    cancellationToken);
            }

            if (success)
            {
                // Обновляем время последнего уведомления
                _lastNotificationTime[Guid.Parse(notificationKey)] = DateTime.UtcNow;

                _logger.LogInformation(
                    "Energy notification sent for user {UserId} (account {GameUid}): {CurrentEnergy}/{MaxEnergy}",
                    notification.User.Id,
                    notification.GameUid,
                    notification.CurrentEnergy,
                    notification.MaxEnergy
                );
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending energy notification for user {UserId}", notification.User.Id);
            return false;
        }
    }

    /// <summary>
    /// Генерирует сообщение об ошибке получения отметки
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Сообщение об ошибке</returns>
    private static string GenerateMarkupFailureMessage(Guid userId)
    {
        return $"⚠️ **Внимание!**\n\n"
            + $"Не удалось поставить отметку в Honkai: Star Rail для пользователя {userId}.\n\n"
            + $"🕐 Время: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC\n"
            + $"📱 Попробуйте проверить настройки аккаунта или обратиться к администратору.\n\n"
            + $"⏰ Следующая попытка автоматической отметки будет через 30 минут."
            + "Вот ссылка для ручной активации - https://act.hoyolab.com/bbs/event/signin/hkrpg/index.html?act_id=e202303301540311";
    }

    /// <summary>
    /// Генерирует сообщение об успешном получении отметки
    /// </summary>
    /// <param name="rewardName">Название награды</param>
    /// <param name="amount">Количество награды</param>
    /// <returns>Сообщение об успехе</returns>
    private static string GenerateMarkupSuccessMessage(string rewardName, int amount)
    {
        return $"Автоотметка была активирована! Награда за сегодня: {rewardName} в количестве {amount} штук!";
    }

    /// <summary>
    /// Генерирует сообщение о том, что награда уже получена
    /// </summary>
    /// <returns>Сообщение о том, что награда уже получена</returns>
    private static string GenerateMarkupAlreadyReceivedMessage()
    {
        return "Награда за отметки для HSR уже была активирована!";
    }

    /// <summary>
    /// Генерирует сообщение об уровне энергии
    /// </summary>
    /// <param name="currentEnergy">Текущий уровень энергии</param>
    /// <param name="maxEnergy">Максимальный уровень энергии</param>
    /// <param name="threshold">Пороговое значение</param>
    /// <param name="recoveryTime">Время до полного восстановления</param>
    /// <returns>Сообщение об уровне энергии</returns>
    private static string GenerateEnergyNotificationMessage(
        int currentEnergy,
        int maxEnergy,
        int threshold,
        TimeSpan recoveryTime)
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

    /// <summary>
    /// Отправляет уведомление через Telegram
    /// </summary>
    /// <param name="telegramId">ID пользователя в Telegram</param>
    /// <param name="message">Сообщение для отправки</param>
    /// <param name="gameUid">UID игрового аккаунта</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    private async Task<bool> SendTelegramNotification(
        long telegramId, 
        string message, 
        int gameUid, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _telegramClient.SendMessage(telegramId, message, cancellationToken: cancellationToken);
            _logger.LogInformation(
                "Telegram notification sent for {TelegramId}: {Message}",
                telegramId,
                message
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Telegram notification for {TelegramId}", telegramId);
            return false;
        }
    }

    /// <summary>
    /// Отправляет уведомление через Twitch
    /// </summary>
    /// <param name="twitchId">ID пользователя в Twitch</param>
    /// <param name="message">Сообщение для отправки</param>
    /// <param name="gameUid">UID игрового аккаунта</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если уведомление отправлено успешно</returns>
    private async Task<bool> SendTwitchNotification(
        string twitchId, 
        string message, 
        int gameUid, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _twitchClient.SendMessageToMainTwitchAsync(message);
            _logger.LogInformation(
                "Twitch notification sent for {TwitchId}: {Message}",
                twitchId,
                message
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Twitch notification for {TwitchId}", twitchId);
            return false;
        }
    }

    /// <summary>
    /// Создает клиент MarchSeven для взаимодействия с API
    /// </summary>
    /// <param name="user">Пользователь с данными аутентификации</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <returns>Настроенный клиент MarchSeven</returns>
    private static MarchSevenClient CreateMarchSevenClient(
        DailyAutoMarkupUser user,
        HttpClient httpClient)
    {
        var cookieV2 = new CookieV2
        {
            LTokenV2 = user.LTokenV2,
            LtMidV2 = user.LtmidV2,
            LtUidV2 = user.LtuidV2,
        };

        var clientData = new ClientData { HttpClient = httpClient, Language = "ru-RU" };

        return MarchSevenClient.Create(cookieV2, clientData);
    }
}