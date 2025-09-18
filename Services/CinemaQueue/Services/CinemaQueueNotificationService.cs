using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;

namespace MARS.Server.Services.CinemaQueue.Services;

public interface ICinemaQueueNotificationService
{
    Task CheckAndNotifyUnwatchedNextItemsAsync(CancellationToken cancellationToken = default);
}

public class CinemaQueueNotificationService(
    ICinemaQueueService cinemaQueueService,
    ITelegramBotClient telegramBotClient, // TODO: Использовать для отправки в Telegram
    ILogger<CinemaQueueNotificationService> logger
) : BackgroundService, ICinemaQueueNotificationService
{
    private static readonly TimeSpan NotificationInterval = TimeSpan.FromDays(3);
    private DateTime _lastNotificationTime = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Cinema Queue Notification Service");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndNotifyUnwatchedNextItemsAsync(stoppingToken);

                // Ждем 1 час перед следующей проверкой
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение при отмене
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Cinema Queue Notification Service");
                // Ждем 30 минут перед повторной попыткой при ошибке
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        logger.LogInformation("Cinema Queue Notification Service stopped");
    }

    public async Task CheckAndNotifyUnwatchedNextItemsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var now = DateTime.UtcNow;

            // Проверяем, прошло ли 3 дня с последнего уведомления
            if (now - _lastNotificationTime < NotificationInterval)
            {
                return;
            }

            logger.LogInformation("Checking for unwatched next items...");

            // Получаем все элементы со статусом Pending, которые помечены как IsNext
            var allItems = await cinemaQueueService.GetAllMediaItemsAsync(cancellationToken);
            var nextItems = allItems
                .Where(item => item is { IsNext: true, Status: MediaStatus.Pending })
                .OrderBy(item => item.CreatedAt)
                .ToList();

            if (nextItems.Count == 0)
            {
                logger.LogInformation("No unwatched next items found");
                return;
            }

            // Фильтруем элементы, которые не просматривались более 3 дней
            var unwatchedItems = nextItems
                .Where(item => now - item.CreatedAt > NotificationInterval)
                .ToList();

            if (unwatchedItems.Count == 0)
            {
                logger.LogInformation("No unwatched next items older than 3 days found");
                return;
            }

            await SendNotificationAsync(unwatchedItems);
            _lastNotificationTime = now;

            logger.LogInformation(
                "Sent notification for {Count} unwatched next items",
                unwatchedItems.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking unwatched next items");
        }
    }

    private async Task SendNotificationAsync(List<CinemaMediaItemDto> unwatchedItems)
    {
        try
        {
            var message = BuildNotificationMessage(unwatchedItems);

            // Отправляем сообщение в Telegram через TelegramExstension.Rxdcodx
            await telegramBotClient.SendMessage(
                chatId: TelegramExstension.Rxdcodx,
                text: message,
                cancellationToken: CancellationToken.None
            );

            logger.LogInformation("Sent Telegram notification: {Message}", message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Telegram notification");
        }
    }

    private static string BuildNotificationMessage(List<CinemaMediaItemDto> unwatchedItems)
    {
        if (unwatchedItems.Count == 1)
        {
            var item = unwatchedItems.First();
            return $"🎬 Напоминание: фильм '{item.Title}' помечен как следующий для просмотра уже более 3 дней! "
                + $"Время смотреть! 👀";
        }

        var titles = unwatchedItems
            .Take(3) // Показываем максимум 3 фильма
            .Select(item => $"'{item.Title}'")
            .ToList();

        var message =
            $"🎬 Напоминание: {unwatchedItems.Count} фильм(ов) помечены как следующие для просмотра уже более 3 дней: "
            + string.Join(", ", titles);

        if (unwatchedItems.Count > 3)
        {
            message += $" и еще {unwatchedItems.Count - 3}...";
        }

        message += " Время смотреть! 👀";

        return message;
    }
}
