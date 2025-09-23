using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.CinemaQueue.Services;

public class TwitchCinemaQueueService(
    ICinemaQueueService cinemaQueueService,
    EventSubWebsocketClient wsClient,
    ILogger<TwitchCinemaQueueService> logger,
    ITwitchClient twitchClient,
    IMediaMetadataService metadataService
) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Twitch Cinema Queue Service");

        // Подписываемся на события Twitch
        wsClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsRedemption;

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Twitch Cinema Queue Service");

        // Отписываемся от событий
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsRedemption;

        return base.StopAsync(cancellationToken);
    }

    private async Task OnChannelPointsRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs e
    )
    {
        try
        {
            logger.LogInformation(
                "Channel points redemption: {RewardTitle} by {UserName}",
                e.Payload.Event.Reward.Title,
                e.Payload.Event.UserName
            );

            // Проверяем, является ли это наградой для добавления в очередь
            if (IsCinemaQueueReward(e.Payload.Event.Reward.Cost))
            {
                await HandleCinemaQueueRedemption(e);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling channel points redemption");
        }
    }

    private static bool IsCinemaQueueReward(int cost)
    {
        // Check if this is a cinema queue reward based on specific cost
        return cost == 1602;
    }

    private async Task HandleCinemaQueueRedemption(ChannelPointsCustomRewardRedemptionArgs e)
    {
        try
        {
            var rewardTitle = e.Payload.Event.Reward.Title;
            var userName = e.Payload.Event.UserName;
            var userId = e.Payload.Event.UserId;
            var userInput = e.Payload.Event.UserInput;

            if (string.IsNullOrWhiteSpace(userInput))
            {
                logger.LogWarning(
                    "User input is empty for reward redemption by {UserName}",
                    userName
                );
                return;
            }

            // Получаем метаданные из ссылки
            var metadata = await metadataService.GetMetadataAsync(userInput);

            string title;
            string? description;

            if (metadata != null)
            {
                title = metadata.Title;
                description = metadata.Description;
                logger.LogInformation("Получены метаданные для {Url}: {Title}", userInput, title);
            }
            else
            {
                // Fallback на случай, если не удалось получить метаданные
                title = $"Requested by {userName}";
                description = $"Added to queue via Twitch reward: {rewardTitle}";
                logger.LogWarning("Не удалось получить метаданные для URL: {Url}", userInput);
            }

            // Создаем запрос на добавление в очередь
            var request = new CreateMediaItemRequest
            {
                Title = title,
                Description = description,
                MediaUrl = userInput,
                AddedBy = userName,
                TwitchUserId = userId,
                TwitchUsername = userName,
                Notes = $"Twitch reward redemption - {DateTime.Now}",
            };

            var mediaItem = await cinemaQueueService.CreateMediaItemAsync(request);
            logger.LogInformation(
                "Added media item to queue via Twitch reward: {Title}",
                mediaItem.Title
            );

            // Отправляем уведомление в чат (можно интегрировать с TwitchClient)
            await SendTwitchNotification(userName, mediaItem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling cinema queue redemption");
        }
    }

    private Task SendTwitchNotification(string userName, CinemaMediaItemDto cinemaMediaItem)
    {
        try
        {
            var message = $"@{userName} добавил '{cinemaMediaItem.Title}' в очередь просмотра! 🎬";
            logger.LogInformation("Twitch notification: {Message}", message);

            twitchClient.SendMessageToMainTwitchAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Twitch notification");
        }

        return Task.CompletedTask;
    }
}
