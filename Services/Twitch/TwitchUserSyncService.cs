using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.TwitchFollowers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;
using User = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace MARS.Server.Services.Twitch;

/// <summary>
/// Сервис для автоматической синхронизации пользователей Twitch из чата
/// </summary>
public class TwitchUserSyncService(
    ITwitchClient twitchClient,
    IDbContextFactory<AppDbContext> dbFactory,
    TwitchUserInfoService userInfoService,
    TokenService tokenService,
    ILogger<TwitchUserSyncService> logger,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Dictionary<string, DateTime> _lastUpdateTime = new();
    private readonly TimeSpan _updateCooldown = TimeSpan.FromMinutes(5);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            twitchClient.OnMessageReceived += OnMessageReceived;
            logger.LogInformation("TwitchUserSyncService started");
        });

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        twitchClient.OnMessageReceived -= OnMessageReceived;
        logger.LogInformation("TwitchUserSyncService stopped");
        return base.StopAsync(cancellationToken);
    }

    private async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        // Пропускаем сообщения не из основного канала
        if (
            !e.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        // Пропускаем ботов и черный список
        if (
            TwitchExstension.BlackList.Logins.Any(t =>
                t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return;
        }

        try
        {
            await ProcessUserAsync(e.ChatMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обработке пользователя {UserId} ({UserName})",
                e.ChatMessage.UserId,
                e.ChatMessage.Username
            );
        }
    }

    private async Task ProcessUserAsync(ChatMessage chatMessage)
    {
        var userId = chatMessage.UserId;
        var userName = chatMessage.Username;
        var displayName = chatMessage.DisplayName;

        // Проверяем cooldown
        if (_lastUpdateTime.TryGetValue(userId, out var lastUpdate))
        {
            if (DateTime.UtcNow - lastUpdate < _updateCooldown)
            {
                return;
            }
        }

        await _semaphore.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();

            // Проверяем, существует ли пользователь
            var existingUser = await db
                .TwitchUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.TwitchId == userId);

            if (existingUser == null)
            {
                // Создаем нового пользователя
                await CreateUserAsync(db, chatMessage);
            }
            else
            {
                // Обновляем существующего пользователя
                await UpdateUserAsync(db, existingUser, chatMessage);
            }

            _lastUpdateTime[userId] = DateTime.UtcNow;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task CreateUserAsync(AppDbContext db, ChatMessage chatMessage)
    {
        var userId = chatMessage.UserId;
        var userName = chatMessage.Username;
        var displayName = chatMessage.DisplayName;

        logger.LogInformation(
            "Создание нового пользователя Twitch: {UserName} (ID: {UserId})",
            userName,
            userId
        );

        // Получаем дополнительную информацию из API
        User? apiUser = null;
        string? chatColor = null;

        if (tokenService.Token?.AccessToken != null)
        {
            try
            {
                var userInfoTask = userInfoService.GetUserInfoAsync(userId);
                var chatColorTask = userInfoService.GetUserChatColorAsync(userId);

                await Task.WhenAll(userInfoTask, chatColorTask);

                apiUser = await userInfoTask;
                chatColor = await chatColorTask;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Не удалось получить информацию из API для пользователя {UserId}",
                    userId
                );
            }
        }

        // Создаем пользователя
        var newUser = new TwitchUser
        {
            TwitchId = userId,
            UserLogin = userName,
            DisplayName = displayName,
            IsModerator = chatMessage.UserDetail.IsModerator,
            IsVip = chatMessage.UserDetail.IsVip,
            ChatColor = chatColor ?? chatMessage.HexColor,
            ProfileImageUrl = apiUser?.ProfileImageUrl,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
        };

        db.TwitchUsers.Add(newUser);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Пользователь Twitch создан: {UserName} (ID: {UserId}), Avatar: {Avatar}",
            userName,
            userId,
            apiUser?.ProfileImageUrl ?? "null"
        );
    }

    private async Task UpdateUserAsync(
        AppDbContext db,
        TwitchUser existingUser,
        ChatMessage chatMessage
    )
    {
        var userId = chatMessage.UserId;
        var userName = chatMessage.Username;
        var displayName = chatMessage.DisplayName;

        var needsUpdate = false;

        // Обновляем базовую информацию
        if (existingUser.UserLogin != userName)
        {
            existingUser.UserLogin = userName;
            needsUpdate = true;
        }

        if (existingUser.DisplayName != displayName)
        {
            existingUser.DisplayName = displayName;
            needsUpdate = true;
        }

        if (existingUser.IsModerator != chatMessage.UserDetail.IsModerator)
        {
            existingUser.IsModerator = chatMessage.UserDetail.IsModerator;
            needsUpdate = true;
        }

        if (existingUser.IsVip != chatMessage.UserDetail.IsVip)
        {
            existingUser.IsVip = chatMessage.UserDetail.IsVip;
            needsUpdate = true;
        }

        // Обновляем цвет чата если изменился
        var newChatColor = chatMessage.HexColor;
        if (existingUser.ChatColor != newChatColor && !string.IsNullOrWhiteSpace(newChatColor))
        {
            existingUser.ChatColor = newChatColor;
            needsUpdate = true;
        }

        // Обновляем аватарку если её нет или если прошло много времени
        if (
            string.IsNullOrWhiteSpace(existingUser.ProfileImageUrl)
            || DateTime.UtcNow - existingUser.LastUpdated > TimeSpan.FromDays(7)
        )
        {
            if (tokenService.Token?.AccessToken != null)
            {
                try
                {
                    var apiUser = await userInfoService.GetUserInfoAsync(userId);

                    if (apiUser != null && !string.IsNullOrWhiteSpace(apiUser.ProfileImageUrl))
                    {
                        existingUser.ProfileImageUrl = apiUser.ProfileImageUrl;
                        needsUpdate = true;

                        logger.LogDebug(
                            "Обновлена аватарка пользователя {UserName} (ID: {UserId}): {Avatar}",
                            userName,
                            userId,
                            apiUser.ProfileImageUrl
                        );
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Не удалось обновить аватарку для пользователя {UserId}",
                        userId
                    );
                }
            }
        }

        if (needsUpdate)
        {
            existingUser.LastUpdated = DateTime.UtcNow;
            db.TwitchUsers.Update(existingUser);
            await db.SaveChangesAsync();

            logger.LogDebug(
                "Пользователь Twitch обновлен: {UserName} (ID: {UserId})",
                userName,
                userId
            );
        }
    }

    protected void Dispose(bool disposing)
    {
        if (disposing)
        {
            _semaphore?.Dispose();
        }

        base.Dispose();
    }
}
