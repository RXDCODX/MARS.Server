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
/// Хранит ID последних 100 сообщений из чата и ID пользователей
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

    private readonly HashSet<string> _allUserIds = new(); // Все ID пользователей для отображения (включая модераторов)
    private readonly SemaphoreSlim _semaphoreSlim = new(1);
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

        if (string.IsNullOrWhiteSpace(e.ChatMessage.UserId))
        {
            return;
        }

        // ID пользователя добавляем всегда (включая модераторов и стримера)
        _semaphoreSlim.Wait();
        _allUserIds.Add(e.ChatMessage.UserId);

        while (_allUserIds.Count > MaxStoredMessages)
        {
            _allUserIds.Remove(e.ChatMessage.UserId);
        }

        _semaphoreSlim.Release();
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

            // Используем все ID пользователей (включая модераторов) для отображения
            _semaphoreSlim.Wait();
            var uniqueUserIds = _allUserIds.ToList();
            _semaphoreSlim.Release();

            logger.LogInformation(
                "MIKU MIKU BEAM: сохранено {UsersCount} уникальных пользователей для отображения",
                uniqueUserIds.Count
            );

            // Обновляем время последней активации
            _lastActivation = now;

            // Получаем информацию о пользователях из базы данных по их ID
            List<TwitchUser> twitchUsers = [];

            if (uniqueUserIds.Count > 0)
            {
                await using var dbContext = await factory.CreateDbContextAsync();

                twitchUsers = await dbContext
                    .TwitchUsers.AsNoTracking()
                    .Where(u => uniqueUserIds.Contains(u.TwitchId))
                    .ToListAsync();

                logger.LogInformation(
                    "MIKU MIKU BEAM: найдено {Count} пользователей в базе данных из {Total}",
                    twitchUsers.Count,
                    uniqueUserIds.Count
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
    /// Удаляет сообщения через Twitch API
    /// </summary>
    public async Task DeleteMessagesAsync()
    {
        if (string.IsNullOrWhiteSpace(tokenService.Token?.AccessToken))
        {
            logger.LogWarning("MIKU MIKU BEAM: отсутствует токен доступа для удаления сообщений");
            return;
        }

        try
        {
            logger.LogInformation("MIKU MIKU BEAM: начинается удаление сообщений");

            await api.Helix.Moderation.DeleteChatMessagesAsync(
                TwitchExstension.ChannelId,
                TwitchExstension.BotId,
                null,
                tokenService.Token?.AccessToken
            );

            logger.LogInformation("MIKU MIKU BEAM: сообщения удалены успешно");
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }

    /// <summary>
    /// Ручная досрочная активация MIKU MIKU BEAM
    /// </summary>
    public async Task<string> ManualActivateAsync()
    {
        var result = string.Empty;

        try
        {
            logger.LogInformation("MIKU MIKU BEAM: ручная активация");

            // Используем все ID пользователей (включая модераторов) для отображения
            _semaphoreSlim.Wait();
            var uniqueUserIds = _allUserIds.ToList();
            _semaphoreSlim.Release();

            logger.LogInformation(
                "MIKU MIKU BEAM: сохранено {UsersCount} уникальных пользователей для отображения",
                uniqueUserIds.Count
            );

            // Обновляем время последней активации
            _lastActivation = DateTimeOffset.Now;

            // Получаем информацию о пользователях из базы данных по их ID
            List<TwitchUser> twitchUsers = [];

            if (uniqueUserIds.Count > 0)
            {
                await using var dbContext = await factory.CreateDbContextAsync();

                twitchUsers = await dbContext
                    .TwitchUsers.AsNoTracking()
                    .Where(u => uniqueUserIds.Contains(u.TwitchId))
                    .ToListAsync();

                logger.LogInformation(
                    "MIKU MIKU BEAM: найдено {Count} пользователей в базе данных из {Total}",
                    twitchUsers.Count,
                    uniqueUserIds.Count
                );
            }

            // Отправляем информацию о пользователях на фронт
            await hubContext.Clients.All.MikuMikuBeam(twitchUsers);

            logger.LogInformation(
                "MIKU MIKU BEAM эффект активирован вручную с {Count} пользователями",
                twitchUsers.Count
            );

            result = $"✅ MIKU MIKU BEAM активирован! Участников: {twitchUsers.Count}";
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result = $"❌ Ошибка при активации MIKU MIKU BEAM: {ex.Message}";
        }

        return result;
    }
}
