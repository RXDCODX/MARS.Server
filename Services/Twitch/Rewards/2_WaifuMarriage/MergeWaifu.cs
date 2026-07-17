using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Validation;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.Entitys;
using MARS.Server.Services.WaifuRoll.helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._2_WaifuMarriage;

public class MergeWaifu(
    ILogger<MergeWaifu> logger,
    ITwitchClient client,
    WaifuRollService waifuRollService,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> factory,
    ITwitchAPI api,
    TokenService tokenService,
    IHostApplicationLifetime lifetime,
    WaifuRollEnsurenceService waifuDbHelper,
    IOptions<ShikimoriClientOptions> options,
    EventSubWebsocketClient wsClient,
    TwitchUserEnsureService twitchUserEnsureService,
    ITwitchEventValidationService validator
) : BackgroundService, ITwitchReward
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    /// <summary>
    /// Обертка семафора с подсчетом использований
    /// </summary>
    private class SemaphoreWrapper
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int UseCount = 0;
    }

    /// <summary>
    /// Словарь семафоров для синхронизации операций по UserId
    /// Предотвращает race condition при создании Husband
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreWrapper> _hostSemaphores = new();

    /// <summary>
    /// Получить или создать семафор для конкретного UserId
    /// </summary>
    private SemaphoreSlim GetOrCreateSemaphore(string userId)
    {
        var wrapper = _hostSemaphores.GetOrAdd(userId, _ => new SemaphoreWrapper());
        Interlocked.Increment(ref wrapper.UseCount);
        return wrapper.Semaphore;
    }

    /// <summary>
    /// Освободить семафор после использования и удалить если больше не используется
    /// </summary>
    private void ReleaseSemaphore(string userId, SemaphoreSlim semaphore)
    {
        semaphore.Release();

        if (_hostSemaphores.TryGetValue(userId, out var wrapper))
        {
            var count = Interlocked.Decrement(ref wrapper.UseCount);

            // Если семафор больше не используется - удаляем и диспозим
            if (count == 0)
            {
                if (_hostSemaphores.TryRemove(userId, out var removedWrapper))
                {
                    removedWrapper.Semaphore.Dispose();
                }
            }
        }
    }

    public bool IsServiceActive { get; set; } = true;
    public int Cost { get; init; } = 2;

    public async Task MergeWaifuTwitchEvent(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var vr = await validator
            .ForRedemption(args)
            .RequireBroadcasterUserId()
            .RequireServiceActive(IsServiceActive)
            .RequireCost(Cost)
            .RequireFollower()
            .ValidateWithResponseAsync(args.Payload.Event.UserName);

        if (vr.IsInvalid)
        {
            return;
        }

        var twEvent = args.Payload.Event;
        var semaphore = GetOrCreateSemaphore(twEvent.UserId);
        await semaphore.WaitAsync(_cancellationToken);

        try
        {
            await using AppDbContext dbContext = await factory.CreateDbContextAsync(
                _cancellationToken
            );

            // Загружаем Husband с TwitchUser
            var host = await dbContext
                .Husbands.Include(h => h.TwitchUser)
                .FirstOrDefaultAsync(h => h.TwitchId == twEvent.UserId, _cancellationToken);

            if (host is not null)
            {
                host.TwitchId = twEvent.UserId;
                // Обновление TwitchUser выполняется отдельным сервисом

                if (!host.IsPrivated)
                {
                    var waifu = await dbContext.Waifus.FindAsync(
                        [host.WaifuRollId],
                        _cancellationToken
                    );
                    if (waifu is { IsPrivated: false })
                    {
                        var isMerged =
                            await waifuRollService.MergeTheWaifu(host, waifu)
                            || twEvent.Id == TwitchExstension.ChannelId;

                        if (isMerged)
                        {
                            var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                                twEvent.UserName,
                                AnswersForTwitchRewards.Answers[Command.MergeWaifu],
                                null,
                                null,
                                waifu
                            );

                            if (string.IsNullOrWhiteSpace(waifu.ImageUrl))
                            {
                                waifu = await waifuDbHelper.EnsureWaifuHaveImageIrl(waifu);
                            }

                            // Убеждаемся, что поля аниме и манги заполнены
                            waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(waifu);

                            dbContext.Waifus.Update(waifu);
                            await dbContext.SaveChangesAsync(_cancellationToken);

                            waifu.IsMerged = true;
                            waifu.ImageUrl = options.Value.ShikimoriSite + waifu.ImageUrl;

                            // Проверяем что TwitchUser загружен
                            if (host.TwitchUser == null)
                            {
                                throw new InvalidOperationException(
                                    $"TwitchUser не найден для Husband {twEvent.UserId}"
                                );
                            }

                            var color = await api.Helix.Chat.GetUserChatColorAsync(
                                [twEvent.UserId]
                            );

                            // Используем аватарку из TwitchUser вместо отдельного запроса к API
                            await hubContext.Clients.All.MergeWaifu(
                                waifu,
                                host,
                                host.TwitchUser.ProfileImageUrl,
                                color.Data[0]?.Color
                            );

                            if (tokenService.Token != null)
                            {
                                await api.Helix.Chat.SendChatAnnouncementAsync(
                                    TwitchExstension.ChannelId,
                                    TwitchExstension.ChannelId,
                                    message,
                                    AnnouncementColors.Primary,
                                    tokenService.Token.AccessToken
                                );
                            }

                            return;
                        }
                    }
                    else if (waifu is { IsPrivated: false })
                    {
                        var tempLate3 = "@{user}, твоя любовь уже занята :-(";
                        var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                            twEvent.UserName,
                            tempLate3
                        );
                        await client.SendMessageToMainTwitchAsync(message, logger);

                        return;
                    }
                    else
                    {
                        var tempLate3 = "@{user}, не удалось найти твою любовь в бд :-(";
                        var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                            twEvent.UserName,
                            tempLate3
                        );
                        await client.SendMessageToMainTwitchAsync(message, logger);

                        return;
                    }
                }
                else
                {
                    var waifu = await dbContext.Waifus.FindAsync(host.WaifuBrideId);

                    if (waifu is { IsPrivated: true })
                    {
                        // Убеждаемся, что изображение и поля заполнены
                        if (string.IsNullOrWhiteSpace(waifu.ImageUrl))
                        {
                            waifu = await waifuDbHelper.EnsureWaifuHaveImageIrl(waifu);
                        }

                        waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(waifu);

                        dbContext.Waifus.Update(waifu);
                        await dbContext.SaveChangesAsync(_cancellationToken);

                        waifu.ImageUrl = options.Value.ShikimoriSite + waifu.ImageUrl;

                        // Проверяем что TwitchUser загружен
                        if (host.TwitchUser == null)
                        {
                            throw new InvalidOperationException(
                                $"TwitchUser не найден для Husband {twEvent.UserId}"
                            );
                        }

                        var color = await api.Helix.Chat.GetUserChatColorAsync([twEvent.UserId]);

                        // Отправляем событие на фронт — длительность брака считается на фронте
                        await hubContext.Clients.All.ShowCurrentWife(
                            waifu,
                            host,
                            host.TwitchUser.ProfileImageUrl,
                            color.Data[0]?.Color
                        );

                        var tempLate = "@{user}, ты уже в браке!";
                        var message9 = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                            twEvent.UserName,
                            tempLate
                        );
                        await client.SendMessageToMainTwitchAsync(message9, logger);
                        return;
                    }

                    var tempLate2 = "@{user}, ты уже помолвен(-а), сорян!";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        twEvent.UserName,
                        tempLate2
                    );
                    await client.SendMessageToMainTwitchAsync(message, logger);
                    return;
                }
            }

            // Гарантируем наличие пользователя в TwitchUsers перед созданием Husband
            await twitchUserEnsureService.EnsureUserExistsAsync(args, _cancellationToken);

            host = new Husband
            {
                TwitchId = twEvent.UserId,
                HusbandCoolDown = new HusbandCoolDown { HusbandId = twEvent.UserId },
                HusbandGreetings = new HusbandAutoHello { HusbandId = twEvent.UserId },
            };

            dbContext.Husbands.Add(host);

            await dbContext.SaveChangesAsync(_cancellationToken);

            var tempLate4 = "@{user}, ты новенький, тебе пока нельзя!";
            var message3 = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                twEvent.UserName,
                tempLate4
            );
            await client.SendMessageToMainTwitchAsync(message3, logger);
            return;
        }
        finally
        {
            ReleaseSemaphore(twEvent.UserId, semaphore);
        }
    }

    public async Task<(Waifu? waifu, Husband? host)> Unmerge(string nickname)
    {
        await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);
        var host = await dbContext
            .Husbands.Include(e => e.TwitchUser)
            .SingleOrDefaultAsync(
                e =>
                    e.TwitchUser != null
                    && EF.Functions.ILike(e.TwitchUser.DisplayName, $"%{nickname}%"),
                _cancellationToken
            );

        if (host is { IsPrivated: true })
        {
            var waifu = await dbContext.Waifus.SingleOrDefaultAsync(
                e => e.ShikiId == host.WaifuBrideId,
                _cancellationToken
            );

            if (waifu is { IsPrivated: true })
            {
                waifu.IsPrivated = false;
                host.WaifuBrideId = null;
                host.IsPrivated = false;
                host.WhenPrivated = null;

                if (
                    dbContext.Entry(waifu).State != EntityState.Modified
                    || dbContext.Entry(host).State != EntityState.Modified
                )
                {
                    dbContext.Waifus.Update(waifu);
                    dbContext.Husbands.Update(host);
                    await twitchUserEnsureService.EnsureUserExistsAsync(
                        host.TwitchId,
                        _cancellationToken
                    );
                }

                await dbContext.SaveChangesAsync(_cancellationToken);

                return (waifu, host);
            }

            return (null, host);
        }

        return (null, null);
    }

    public async Task<(Waifu? waifu, Husband? host)> Unmerge(int id)
    {
        await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);

        var host = await dbContext.Husbands.SingleOrDefaultAsync(
            e => e.TwitchId == id.ToString(),
            _cancellationToken
        );

        if (host is { IsPrivated: true })
        {
            var waifu = await dbContext.Waifus.SingleOrDefaultAsync(
                e => e.ShikiId == host.WaifuBrideId,
                _cancellationToken
            );

            if (waifu is { IsPrivated: true })
            {
                waifu.IsPrivated = false;
                host.WaifuBrideId = null;
                host.IsPrivated = false;
                host.WhenPrivated = null;

                if (
                    dbContext.Entry(waifu).State != EntityState.Modified
                    || dbContext.Entry(host).State != EntityState.Modified
                )
                {
                    dbContext.Waifus.Update(waifu);
                    dbContext.Husbands.Update(host);
                }

                await dbContext.SaveChangesAsync(_cancellationToken);

                return (waifu, host);
            }

            return (null, host);
        }

        return (null, null);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd += MergeWaifuTwitchEvent;
        }

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= MergeWaifuTwitchEvent;
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        // Освобождаем все семафоры при уничтожении сервиса
        foreach (var wrapper in _hostSemaphores.Values)
        {
            wrapper?.Semaphore.Dispose();
        }
        _hostSemaphores.Clear();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
