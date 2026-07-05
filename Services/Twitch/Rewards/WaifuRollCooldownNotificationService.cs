using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Validation;
using MARS.Server.Services.WaifuRoll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards;

public class WaifuRollCooldownNotificationService(
    IHostApplicationLifetime lifetime,
    EventSubWebsocketClient eventSub,
    ITwitchClient twitchClient,
    WaifuRollService waifuRollService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<WaifuRollCooldownNotificationService> logger,
    ITwitchEventValidationService validator
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
        var result = await validator
            .ForRedemption(e)
            .RequireBroadcasterUserId()
            .RequireCost(WaifuRollCost)
            .ValidateAsync();

        if (result.IsInvalid)
        {
            await twitchClient.SendMessageToMainTwitchAsync($"@{e.Payload.Event.UserName}, " + result.FirstError);
            return;
        }

        var twEvent = e.Payload.Event;

        try
        {
            await _semaphore.WaitAsync();
            _pendingNotifications.Remove(twEvent.UserId);
            _notifiedUsers.Remove(twEvent.UserId);
            _semaphore.Release();

            await Task.Delay(TimeSpan.FromSeconds(2));

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var host = await dbContext
                .Husbands.Include(h => h.HusbandCoolDown)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.TwitchId == twEvent.UserId);

            if (host?.HusbandCoolDown == null)
            {
                return;
            }

            var cooldownTime = host.HusbandCoolDown.Time.ToOffset(TimeSpan.FromHours(3));
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
        var result = await validator
            .ForMessageReceived(e)
            .RequireChannel()
            .SkipBlacklisted()
            .RequireUserId()
            .ValidateAsync();

        if (result.IsInvalid)
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
                .Husbands.Include(h => h.HusbandCoolDown)
                .Include(h => h.HusbandGreetings)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.TwitchId == e.ChatMessage.UserId);

            if (host?.HusbandCoolDown == null)
            {
                _pendingNotifications.Remove(e.ChatMessage.UserId);
                _semaphore.Release();
                return;
            }

            var cooldownTime = host.HusbandCoolDown.Time.ToOffset(TimeSpan.FromHours(3));
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
