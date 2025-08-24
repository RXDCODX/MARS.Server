using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.CinemaQueue.Interfaces;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.CinemaQueue.Services;

public class TwitchCinemaQueueService(
    ICinemaQueueService cinemaQueueService,
    EventSubWebsocketClient wsClient,
    ILogger<TwitchCinemaQueueService> logger,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Twitch Cinema Queue Service");

        // Подписываемся на события Twitch
        wsClient.ChannelPointsCustomRewardRedemption += OnChannelPointsRedemption;
        wsClient.ChannelFollow += OnChannelFollow;
        wsClient.ChannelSubscribe += OnChannelSubscribe;

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Twitch Cinema Queue Service");

        // Отписываемся от событий
        wsClient
        wsClient.ChannelPointsCustomRewardRedemption -= OnChannelPointsRedemption;
        wsClient.ChannelFollow -= OnChannelFollow;
        wsClient.ChannelSubscribe -= OnChannelSubscribe;

        return base.StopAsync(cancellationToken);
    }

    private async void OnChannelPointsRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs e
    )
    {
        try
        {
            logger.LogInformation(
                "Channel points redemption: {RewardTitle} by {UserName}",
                e.Notification.Event.Reward.Title,
                e.Notification.Event.UserName
            );

            // Проверяем, является ли это наградой для добавления в очередь
            if (
                IsCinemaQueueReward(
                    e.Notification.Event.Reward.Title,
                    e.Notification.Event.Reward.Cost
                )
            )
            {
                await HandleCinemaQueueRedemption(e);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling channel points redemption");
        }
    }

    private async void OnChannelFollow(object? sender, ChannelFollowArgs e)
    {
        try
        {
            logger.LogInformation("New follower: {UserName}", e.Notification.Event.UserName);

            // Автоматически добавляем фильм/сериал для нового фолловера
            await AddWelcomeMediaItem(e.Notification.Event.UserName, e.Notification.Event.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling channel follow");
        }
    }

    private async void OnChannelSubscribe(object? sender, ChannelSubscribeArgs e)
    {
        try
        {
            logger.LogInformation("New subscriber: {UserName}", e.Notification.Event.UserName);

            // Автоматически добавляем премиум контент для нового подписчика
            await AddPremiumMediaItem(e.Notification.Event.UserName, e.Notification.Event.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling channel subscribe");
        }
    }

    private bool IsCinemaQueueReward(string rewardTitle, int cost)
    {
        // Check if this is a cinema queue reward based on specific cost
        return cost == 1602;
    }

    private async Task HandleCinemaQueueRedemption(ChannelPointsCustomRewardRedemptionArgs e)
    {
        try
        {
            var rewardTitle = e.Notification.Event.Reward.Title.ToLowerInvariant();
            var userName = e.Notification.Event.UserName;
            var userId = e.Notification.Event.UserId;

            // Определяем тип медиа по названию награды
            var mediaType = DetermineMediaType(rewardTitle);

            // Создаем запрос на добавление в очередь
            var request = new CreateMediaItemRequest
            {
                Title = $"Requested by {userName}",
                Description =
                    $"Added to queue via Twitch reward: {e.Notification.Event.Reward.Title}",
                Type = mediaType,
                Priority = GetPriorityByReward(e.Notification.Event.Reward.Cost),
                AddedBy = userName,
                TwitchUserId = userId,
                TwitchUsername = userName,
                Notes = $"Twitch reward redemption - {e.Notification.Event.Reward.Title}",
                EpisodeNumber = 1,
                DurationMinutes = 0,
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
                Type = MediaType.Special,
                Priority = 1,
                AddedBy = "System",
                TwitchUserId = userId,
                TwitchUsername = userName,
                Notes = "Automatically added for new follower",
                EpisodeNumber = 1,
                DurationMinutes = 0,
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
                Type = MediaType.Special,
                Priority = 2,
                AddedBy = "System",
                TwitchUserId = userId,
                TwitchUsername = userName,
                Notes = "Automatically added for new subscriber",
                EpisodeNumber = 1,
                DurationMinutes = 0,
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

    private MediaType DetermineMediaType(string rewardTitle)
    {
        if (rewardTitle.Contains("movie") || rewardTitle.Contains("фильм"))
        {
            return MediaType.Movie;
        }

        if (rewardTitle.Contains("series") || rewardTitle.Contains("сериал"))
        {
            return MediaType.Series;
        }

        if (rewardTitle.Contains("anime") || rewardTitle.Contains("аниме"))
        {
            return MediaType.Anime;
        }

        if (rewardTitle.Contains("documentary") || rewardTitle.Contains("документал"))
        {
            return MediaType.Documentary;
        }

        return MediaType.Movie; // По умолчанию
    }

    private int GetPriorityByReward(int rewardCost)
    {
        // Чем дороже награда, тем выше приоритет
        return rewardCost switch
        {
            >= 1000 => 5,
            >= 500 => 4,
            >= 200 => 3,
            >= 100 => 2,
            _ => 1,
        };
    }

    private async Task SendTwitchNotification(string userName, MediaItemDto mediaItem)
    {
        try
        {
            // Здесь можно интегрировать с TwitchClient для отправки сообщений в чат
            var message = $"@{userName} добавил '{mediaItem.Title}' в очередь просмотра! 🎬";
            logger.LogInformation("Twitch notification: {Message}", message);

            // TODO: Интеграция с TwitchClient для отправки сообщения в чат
            // await _twitchClient.SendMessageAsync(channel, message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Twitch notification");
        }
    }
}
