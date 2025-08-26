using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.CinemaQueue.Services;

public class TwitchCinemaQueueService(
    ICinemaQueueService cinemaQueueService,
    EventSubWebsocketClient wsClient,
    ILogger<TwitchCinemaQueueService> logger,
    IHostApplicationLifetime lifetime,
    ITwitchClient twitchClient
) : BackgroundService
{
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Twitch Cinema Queue Service");

        // Подписываемся на события Twitch
        wsClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsRedemption;
        wsClient.ChannelFollow += OnChannelFollow;
        wsClient.ChannelSubscribe += OnChannelSubscribe;

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Twitch Cinema Queue Service");

        // Отписываемся от событий
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsRedemption;
        wsClient.ChannelFollow -= OnChannelFollow;
        wsClient.ChannelSubscribe -= OnChannelSubscribe;

        return base.StopAsync(cancellationToken);
    }

    private async Task OnChannelPointsRedemption(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs e
    )
    {
        try
        {
            logger.LogInformation(
                "Channel points redemption: {RewardTitle} by {UserName}",
                e.Notification.Payload.Event.Reward.Title,
                e.Notification.Payload.Event.UserName
            );

            // Проверяем, является ли это наградой для добавления в очередь
            if (IsCinemaQueueReward(e.Notification.Payload.Event.Reward.Cost))
            {
                await HandleCinemaQueueRedemption(e);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling channel points redemption");
        }
    }

    private async Task OnChannelFollow(object sender, ChannelFollowArgs e)
    {
        try
        {
            logger.LogInformation(
                "New follower: {UserName}",
                e.Notification.Payload.Event.UserName
            );

            // Автоматически добавляем фильм/сериал для нового фолловера
            await AddWelcomeMediaItem(
                e.Notification.Payload.Event.UserName,
                e.Notification.Payload.Event.UserId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling channel follow");
        }
    }

    private async Task OnChannelSubscribe(object sender, ChannelSubscribeArgs e)
    {
        try
        {
            logger.LogInformation(
                "New subscriber: {UserName}",
                e.Notification.Payload.Event.UserName
            );

            // Автоматически добавляем премиум контент для нового подписчика
            await AddPremiumMediaItem(
                e.Notification.Payload.Event.UserName,
                e.Notification.Payload.Event.UserId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling channel subscribe");
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
            var rewardTitle = e.Notification.Payload.Event.Reward.Title;
            var userName = e.Notification.Payload.Event.UserName;
            var userId = e.Notification.Payload.Event.UserId;

            // Создаем запрос на добавление в очередь
            var request = new CreateMediaItemRequest
            {
                Title = $"Requested by {userName}",
                Description =
                    $"Added to queue via Twitch reward: {e.Notification.Payload.Event.Reward.Title}",
                MediaUrl = $"https://example.com/media/{Guid.NewGuid()}", // Заглушка, в реальности нужно получать от пользователя
                AddedBy = userName,
                TwitchUserId = userId,
                TwitchUsername = userName,
                Notes = $"Twitch reward redemption - {e.Notification.Payload.Event.Reward.Title}",
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

    private async Task AddWelcomeMediaItem(string userName, string userId)
    {
        try
        {
            var request = new CreateMediaItemRequest
            {
                Title = $"Welcome {userName}",
                Description = "Welcome gift for new follower",
                MediaUrl = $"https://example.com/welcome/{Guid.NewGuid()}", // Заглушка
                Priority = 1,
                AddedBy = "System",
                TwitchUserId = userId,
                TwitchUsername = userName,
                Notes = "Automatically added for new follower",
            };

            var mediaItem = await cinemaQueueService.CreateMediaItemAsync(request);
            logger.LogInformation(
                "Added welcome media item for new follower: {UserName}",
                userName
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding welcome media item for {UserName}", userName);
        }
    }

    private async Task AddPremiumMediaItem(string userName, string userId)
    {
        try
        {
            var request = new CreateMediaItemRequest
            {
                Title = $"Premium Content for {userName}",
                Description = "Premium content for new subscriber",
                MediaUrl = $"https://example.com/premium/{Guid.NewGuid()}", // Заглушка
                Priority = 2,
                AddedBy = "System",
                TwitchUserId = userId,
                TwitchUsername = userName,
                Notes = "Automatically added for new subscriber",
            };

            var mediaItem = await cinemaQueueService.CreateMediaItemAsync(request);
            logger.LogInformation(
                "Added premium media item for new subscriber: {UserName}",
                userName
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding premium media item for {UserName}", userName);
        }
    }

    private Task SendTwitchNotification(string userName, MediaItemDto mediaItem)
    {
        try
        {
            var message = $"@{userName} добавил '{mediaItem.Title}' в очередь просмотра! 🎬";
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
