using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._1580_MikuBeam;

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
    IDbContextFactory<AppDbContext> factory,
    RickRollerService rickRollerService,
    MikuBeam_TwitchReward reward
) : BackgroundService
{
    private readonly HashSet<string> _allUserIds = []; // Все ID пользователей для отображения (включая модераторов)
    private readonly HashSet<string> _moderatorIds = []; // ID модераторов
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

    private Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (
            !e.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return Task.CompletedTask;
        }

        if (
            TwitchExstension.BlackList.Logins.Any(u =>
                u.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(e.ChatMessage.UserId))
        {
            return Task.CompletedTask;
        }

        // ID пользователя добавляем всегда (включая модераторов и стримера)
        _semaphoreSlim.Wait();
        _allUserIds.Add(e.ChatMessage.UserId);

        // Отслеживаем модераторов
        if (e.ChatMessage.UserDetail.IsModerator)
        {
            _moderatorIds.Add(e.ChatMessage.UserId);
        }

        while (_allUserIds.Count > MaxStoredMessages)
        {
            _allUserIds.Remove(e.ChatMessage.UserId);
        }

        _semaphoreSlim.Release();

        return Task.CompletedTask;
    }

    private async Task OnChannelPointsCustomRewardRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;

        if (
            twEvent.Reward.Cost != reward.Cost
            || !twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        await rickRollerService.TryRickRollAsync(
            TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
            async () =>
            {
                // Проверка кулдауна
                var now = DateTimeOffset.Now;
                var timeSinceLastActivation = now - _lastActivation;
                if (timeSinceLastActivation.TotalSeconds < CooldownSeconds)
                {
                    var remainingSeconds =
                        CooldownSeconds - (int)timeSinceLastActivation.TotalSeconds;
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
                    await _semaphoreSlim.WaitAsync();
                    var uniqueUserIds = _allUserIds.ToList();
                    _semaphoreSlim.Release();

                    logger.LogInformation(
                        "MIKU MIKU BEAM: найдено {UsersCount} уникальных пользователей для отображения",
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
        );
    }

    /// <summary>
    /// Удаляет все сообщения в чате через Twitch API и отправляет юзеров в 1-секундный таймаут
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
            // Получаем список юзеров для таймаута (исключаем модераторов)
            await _semaphoreSlim.WaitAsync();
            var usersToTimeout = _allUserIds
                .Where(userId => !_moderatorIds.Contains(userId))
                .ToList();
            _semaphoreSlim.Release();

            if (usersToTimeout.Count > 0)
            {
                logger.LogInformation(
                    "MIKU MIKU BEAM: отправляем {Count} юзеров в 1-секундный таймаут",
                    usersToTimeout.Count
                );

                // Отправляем каждого юзера в 1-секундный таймаут
                foreach (var userId in usersToTimeout)
                {
                    try
                    {
                        await api.Helix.Moderation.BanUserAsync(
                            TwitchExstension.ChannelId,
                            TwitchExstension.ChannelId,
                            new BanUserRequest
                            {
                                Duration = 5,
                                Reason = "MIKU MIKU BEAM",
                                UserId = userId,
                            },
                            tokenService.Token.AccessToken
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "MIKU MIKU BEAM: не удалось отправить юзера {UserId} в таймаут",
                            userId
                        );
                    }
                }

                logger.LogInformation("MIKU MIKU BEAM: юзеры успешно отправлены в таймаут");
            }
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
            await _semaphoreSlim.WaitAsync();
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
