using System.Collections.Generic;
using System.Threading;
using MARS.Server.Services.WaifuRoll;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Rewards;

public class WaifuRollCooldownNotificationService(
    IHostApplicationLifetime lifetime,
    EventSubWebsocketClient eventSub,
    ITwitchClient twitchClient,
    WaifuRollService waifuRollService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<WaifuRollCooldownNotificationService> logger
) : BackgroundService
{
    private const int WaifuRollCost = 4;
    private readonly Dictionary<string, DateTimeOffset> _pendingNotifications = new();
    private readonly HashSet<string> _notifiedUsers = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            eventSub.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemptionAdd;
            twitchClient.OnMessageReceived += OnMessageReceived;
            logger.LogInformation("WaifuRollCooldownNotificationService запущен");
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            eventSub.ChannelPointsCustomRewardRedemptionAdd -=
                OnChannelPointsCustomRewardRedemptionAdd;
            twitchClient.OnMessageReceived -= OnMessageReceived;
            logger.LogInformation("WaifuRollCooldownNotificationService остановлен");
        });

        return Task.CompletedTask;
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs e
    )
    {
        var twEvent = e.Payload.Event;
        if (
            !twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        if (twEvent.Reward.Cost != WaifuRollCost)
        {
            return;
        }

        try
        {
            await _semaphore.WaitAsync();
            _pendingNotifications.Remove(twEvent.UserId);
            _notifiedUsers.Remove(twEvent.UserId);
            _semaphore.Release();

            await Task.Delay(TimeSpan.FromSeconds(2));

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var host = await dbContext
                .Hosts.Include(h => h.HostCoolDown)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.TwitchId == twEvent.UserId);

            if (host?.HostCoolDown == null)
            {
                return;
            }

            var cooldownTime = host.HostCoolDown.Time.ToOffset(TimeSpan.FromHours(3));
            var now = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
            var timeSinceCooldownUpdate = now - cooldownTime;

            if (timeSinceCooldownUpdate > TimeSpan.FromMinutes(1))
            {
                return;
            }

            var cooldown = await waifuRollService.GetWaifuRollCoolDownAsync();
            var cooldownEnd = cooldownTime.Add(cooldown);

            await _semaphore.WaitAsync();
            try
            {
                if (!_notifiedUsers.Contains(twEvent.UserId))
                {
                    _pendingNotifications[twEvent.UserId] = cooldownEnd;
                    logger.LogDebug(
                        "Добавлен в ожидание уведомления: {UserId}, кулдаун закончится: {CooldownEnd}",
                        twEvent.UserId,
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
                "Ошибка при обработке использования waifuroll для {UserId}",
                twEvent.UserId
            );
        }
    }

    private async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
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
            TwitchExstension.BlackList.Any(t =>
                t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.ChatMessage.UserId))
        {
            return;
        }

        try
        {
            await _semaphore.WaitAsync();

            if (
                !_pendingNotifications.TryGetValue(
                    e.ChatMessage.UserId,
                    out DateTimeOffset cooldownEndTime
                )
            )
            {
                _semaphore.Release();
                return;
            }

            var now = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));

            if (now < cooldownEndTime)
            {
                _semaphore.Release();
                return;
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var host = await dbContext
                .Hosts.Include(h => h.HostCoolDown)
                .Include(h => h.HostGreetings)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.TwitchId == e.ChatMessage.UserId);

            if (host?.HostCoolDown == null)
            {
                _pendingNotifications.Remove(e.ChatMessage.UserId);
                _semaphore.Release();
                return;
            }

            var cooldownTime = host.HostCoolDown.Time.ToOffset(TimeSpan.FromHours(3));
            var cooldown = await waifuRollService.GetWaifuRollCoolDownAsync();
            var cooldownEnd = cooldownTime.Add(cooldown);

            if (now < cooldownEnd)
            {
                _pendingNotifications.Remove(e.ChatMessage.UserId);
                _semaphore.Release();
                return;
            }

            _pendingNotifications.Remove(e.ChatMessage.UserId);
            _notifiedUsers.Add(e.ChatMessage.UserId);

            if (_notifiedUsers.Count > 1000)
            {
                _notifiedUsers.Clear();
            }

            _semaphore.Release();

            var message =
                $"@{e.ChatMessage.Username}, кулдаун на WaifuRoll прошел! Можешь использовать снова! 🎉";
            await twitchClient.SendMessageToMainTwitchAsync(message, logger);
            logger.LogInformation(
                "Отправлено уведомление о завершении кулдауна для {Username} ({UserId})",
                e.ChatMessage.Username,
                e.ChatMessage.UserId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обработке сообщения от {Username} ({UserId})",
                e.ChatMessage.Username,
                e.ChatMessage.UserId
            );
            _semaphore.Release();
        }
    }

    public override void Dispose()
    {
        _semaphore?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
