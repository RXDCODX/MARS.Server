using System.Collections.Concurrent;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchMikuMikuBeamReward;

/// <summary>
/// Сервис для обработки награды "MIKU MIKU BEAM" на Twitch
/// Хранит ID последних 100 сообщений из чата с никнеймами пользователей
/// </summary>
public class TwitchMikuBeamRewardService(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    ITwitchClient client,
    ITwitchAPI api,
    TokenService tokenService,
    ILogger<TwitchMikuBeamRewardService> logger,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    IDbContextFactory<AppDbContext> factory
) : BackgroundService, ITwitchReward
{
    public bool IsServiceActive { get; set; } = true;
    public int Cost { get; init; } = 1580;

    private readonly ConcurrentQueue<string> _messageIdsToDelete = new(); // ID сообщений для удаления
    private readonly ConcurrentQueue<string> _allUsernames = new(); // Все логины для отображения (включая модераторов)
    private DateTimeOffset _lastActivation = DateTimeOffset.MinValue;
    private const int MaxStoredMessages = 100;
    private const int CooldownSeconds = 60;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += OnMessageReceived;
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            client.OnMessageReceived -= OnMessageReceived;
            wsClient.ChannelPointsCustomRewardRedemptionAdd -=
                OnChannelPointsCustomRewardRedemption;
        });

        return Task.CompletedTask;
    }

    private void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (!IsServiceActive)
        {
            return;
        }

        if (
            !e.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        if (
            TwitchExstension.BlackList.Any(u =>
                u.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.ChatMessage.Username))
        {
            return;
        }

        // Логин добавляем всегда (включая модераторов и стримера)
        _allUsernames.Enqueue(e.ChatMessage.Username);

        while (_allUsernames.Count > MaxStoredMessages)
        {
            _allUsernames.TryDequeue(out _);
        }

        // ID сообщения сохраняем только для обычных пользователей (не модераторов и не стримера)
        var isModeratorOrBroadcaster = e.ChatMessage.IsModerator || e.ChatMessage.IsBroadcaster;

        if (!isModeratorOrBroadcaster && !string.IsNullOrWhiteSpace(e.ChatMessage.Id))
        {
            _messageIdsToDelete.Enqueue(e.ChatMessage.Id);

            while (_messageIdsToDelete.Count > MaxStoredMessages)
            {
                _messageIdsToDelete.TryDequeue(out _);
            }
        }
    }

    private async Task OnChannelPointsCustomRewardRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        if (!IsServiceActive)
        {
            return;
        }

        var twEvent = args.Payload.Event;

        if (
            twEvent.Reward.Cost != Cost
            || !twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        // Проверка кулдауна
        var now = DateTimeOffset.Now;
        var timeSinceLastActivation = now - _lastActivation;
        if (timeSinceLastActivation.TotalSeconds < CooldownSeconds)
        {
            var remainingSeconds = CooldownSeconds - (int)timeSinceLastActivation.TotalSeconds;
            logger.LogInformation(
                "MIKU MIKU BEAM: кулдаун активен, осталось {Seconds} секунд",
                remainingSeconds
            );
            await client.SendMessageToMainTwitchAsync(
                $"@{twEvent.UserName}, кулдаун MIKU MIKU BEAM! Осталось {remainingSeconds} секунд.",
                logger
            );
            return;
        }

        try
        {
            logger.LogInformation(
                "MIKU MIKU BEAM награда активирована пользователем {UserName} за {Cost} баллов",
                twEvent.UserName,
                twEvent.Reward.Cost
            );

            // Используем все логины (включая модераторов) для отображения
            var allUsernamesCopy = _allUsernames.ToList();
            var uniqueUsernames = allUsernamesCopy.Distinct().ToList();

            var messageIdsCopy = _messageIdsToDelete.ToList();

            logger.LogInformation(
                "MIKU MIKU BEAM: сохранено {MessagesCount} сообщений для удаления и {UsersCount} уникальных пользователей для отображения",
                messageIdsCopy.Count,
                uniqueUsernames.Count
            );

            // Обновляем время последней активации
            _lastActivation = now;

            // Получаем информацию о пользователях из базы данных
            List<TwitchUser> twitchUsers = [];

            if (uniqueUsernames.Count > 0)
            {
                await using var dbContext = await factory.CreateDbContextAsync();

                twitchUsers = await dbContext
                    .TwitchUsers.AsNoTracking()
                    .Where(u => uniqueUsernames.Contains(u.UserLogin))
                    .Distinct()
                    .ToListAsync();

                logger.LogInformation(
                    "MIKU MIKU BEAM: найдено {Count} пользователей в базе данных из {Total}",
                    twitchUsers.Count,
                    uniqueUsernames.Count
                );
            }

            // Отправляем информацию о пользователях на фронт
            await hubContext.Clients.All.MikuMikuBeam(twitchUsers);

            logger.LogInformation(
                "MIKU MIKU BEAM эффект активирован для пользователя {UserName} с {Count} пользователями",
                twEvent.UserName,
                twitchUsers.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }

    /// <summary>
    /// Массово удаляет сообщения через Twitch API
    /// </summary>
    public async Task DeleteMessagesAsync()
    {
        var messageIds = _messageIdsToDelete.ToList();

        if (messageIds.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(tokenService.Token?.AccessToken))
        {
            logger.LogWarning("MIKU MIKU BEAM: отсутствует токен доступа для удаления сообщений");
            return;
        }

        try
        {
            logger.LogInformation(
                "MIKU MIKU BEAM: начинается удаление {Count} сообщений",
                messageIds.Count
            );

            var deleteTasks = messageIds.Select(DeleteSingleMessageAsync);

            await Task.WhenAll(deleteTasks);

            logger.LogInformation("MIKU MIKU BEAM: все сообщения удалены успешно");
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }

    /// <summary>
    /// Удаляет одно сообщение через Twitch API
    /// </summary>
    private async Task DeleteSingleMessageAsync(string messageId)
    {
        try
        {
            await api.Helix.Moderation.DeleteChatMessagesAsync(
                TwitchExstension.ChannelId,
                TwitchExstension.BotId,
                messageId,
                tokenService.Token?.AccessToken
            );

            logger.LogDebug("MIKU MIKU BEAM: сообщение {MessageId} удалено", messageId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "MIKU MIKU BEAM: ошибка удаления сообщения {MessageId}",
                messageId
            );
        }
    }
}
