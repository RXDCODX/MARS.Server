using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Validation;
using MARS.Server.Services.WaifuRoll;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards;

public class RollCooldownNotificationService(
    IHostApplicationLifetime lifetime,
    EventSubWebsocketClient eventSub,
    ITwitchClient twitchClient,
    WaifuRollService waifuRollService,
    RollCooldownService cooldownService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<RollCooldownNotificationService> logger,
    ITwitchEventValidationService validator
) : BackgroundService
{
    private const int RollCost = 4;

    private static readonly Dictionary<string, string> RollTypeNames = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["Waifu"] = "WaifuRoll",
        ["Miku"] = "MikuRoll",
        ["Fumo"] = "FumoRoll",
        ["Frog"] = "FrogRoll",
    };

    private readonly Dictionary<(string UserId, string RollType), DateTime> _pendingNotifications =
        new();
    private readonly HashSet<(string UserId, string RollType)> _notifiedUsers = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            eventSub.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemptionAdd;
            twitchClient.OnMessageReceived += OnMessageReceived;
            logger.LogInformation("RollCooldownNotificationService запущен");
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            eventSub.ChannelPointsCustomRewardRedemptionAdd -=
                OnChannelPointsCustomRewardRedemptionAdd;
            twitchClient.OnMessageReceived -= OnMessageReceived;
            logger.LogInformation("RollCooldownNotificationService остановлен");
        });

        return Task.CompletedTask;
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs e
    )
    {
        var result = await validator
            .ForRedemption(e)
            .RequireBroadcasterUserId()
            .RequireCost(RollCost)
            .RequireFollower()
            .ValidateWithResponseAsync(e.Payload.Event.UserName);

        if (result.IsInvalid)
        {
            return;
        }

        var twEvent = e.Payload.Event;

        try
        {
            await _semaphore.WaitAsync();
            try
            {
                // Clear all pending notifications for this user (any roll type)
                var keysToRemove = _pendingNotifications
                    .Keys.Where(k => k.UserId == twEvent.UserId)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _pendingNotifications.Remove(key);
                    _notifiedUsers.Remove(key);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            // Wait for reward handler to process and set cooldown
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Check which roll type was just used by looking at RollCooldowns
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var recentCooldown = await dbContext
                .RollCooldowns.AsNoTracking()
                .Where(r => r.TwitchUserId == twEvent.UserId)
                .OrderByDescending(r => r.LastRollTime)
                .FirstOrDefaultAsync();

            if (recentCooldown is null)
            {
                return;
            }

            var timeSinceRoll = DateTime.Now - recentCooldown.LastRollTime;
            if (timeSinceRoll > TimeSpan.FromMinutes(1))
            {
                return;
            }

            // Get cooldown duration for this roll type
            var cooldown = await GetCooldownForRollType(recentCooldown.RollType);
            var cooldownEnd = recentCooldown.LastRollTime.Add(cooldown);

            await _semaphore.WaitAsync();
            try
            {
                var key = (twEvent.UserId, recentCooldown.RollType);
                if (!_notifiedUsers.Contains(key))
                {
                    _pendingNotifications[key] = cooldownEnd;
                    logger.LogDebug(
                        "Добавлен в ожидание уведомления: {UserId} ({RollType}), кулдаун закончится: {CooldownEnd}",
                        twEvent.UserId,
                        recentCooldown.RollType,
                        cooldownEnd
                    );
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обработке использования roll для {UserId}",
                twEvent.UserId
            );
        }
    }

    private async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var result = await validator
            .ForMessageReceived(e)
            .RequireChannel()
            .SkipBlacklisted()
            .RequireUserId()
            .RequireFollower()
            .ValidateWithResponseAsync(e.ChatMessage.Username);

        if (result.IsInvalid)
        {
            return;
        }

        try
        {
            await _semaphore.WaitAsync();

            // Find all pending notifications for this user
            var userKeys = _pendingNotifications
                .Keys.Where(k => k.UserId == e.ChatMessage.UserId)
                .ToList();

            if (userKeys.Count == 0)
            {
                _semaphore.Release();
                return;
            }

            var now = DateTime.Now;
            var expiredKeys = new List<(string UserId, string RollType)>();

            foreach (var key in userKeys)
            {
                if (now >= _pendingNotifications[key])
                {
                    expiredKeys.Add(key);
                }
            }

            if (expiredKeys.Count == 0)
            {
                _semaphore.Release();
                return;
            }

            // Verify cooldowns are actually expired in DB
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            foreach (var key in expiredKeys)
            {
                var cooldownRecord = await dbContext
                    .RollCooldowns.AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        r.TwitchUserId == key.UserId && r.RollType == key.RollType
                    );

                if (cooldownRecord is null)
                {
                    _pendingNotifications.Remove(key);
                    continue;
                }

                var cooldown = await GetCooldownForRollType(key.RollType);
                var cooldownEnd = cooldownRecord.LastRollTime.Add(cooldown);

                if (now < cooldownEnd)
                {
                    // Cooldown not actually expired yet, update pending time
                    _pendingNotifications[key] = cooldownEnd;
                    continue;
                }

                _pendingNotifications.Remove(key);
                _notifiedUsers.Add(key);

                if (_notifiedUsers.Count > 1000)
                {
                    _notifiedUsers.Clear();
                }

                var rollName = RollTypeNames.GetValueOrDefault(key.RollType, key.RollType);
                var message =
                    $"@{e.ChatMessage.Username}, кулдаун на {rollName} прошел! Можешь использовать снова! 🎉";
                await twitchClient.SendMessageToMainTwitchAsync(message, logger);
                logger.LogInformation(
                    "Отправлено уведомление о завершении кулдауна {RollType} для {Username} ({UserId})",
                    key.RollType,
                    e.ChatMessage.Username,
                    e.ChatMessage.UserId
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обработке сообщения от {Username} ({UserId})",
                e.ChatMessage.Username,
                e.ChatMessage.UserId
            );
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<TimeSpan> GetCooldownForRollType(string rollType)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var key = $"RootState_{rollType}RollCooldownMinutes";
            var state = await dbContext
                .RootState.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == key);

            if (state is not null && int.TryParse(state.Value, out var minutes) && minutes > 0)
            {
                return TimeSpan.FromMinutes(minutes);
            }
        }
        catch
        {
            // Fall through to default
        }

        return TimeSpan.FromMinutes(20);
    }

    public override void Dispose()
    {
        _semaphore?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
