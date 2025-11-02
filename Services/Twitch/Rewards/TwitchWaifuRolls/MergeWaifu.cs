using System.Collections.Concurrent;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

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
    TwitchUserEnsureService twitchUserEnsureService
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
    /// Предотвращает race condition при создании Host
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
        var twEvent = args.Payload.Event;
        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            ) && IsServiceActive
        )
        {
            if (twEvent.Reward.Cost == Cost)
            {
                var semaphore = GetOrCreateSemaphore(twEvent.UserId);
                await semaphore.WaitAsync(_cancellationToken);

                try
                {
                    await using AppDbContext dbContext = await factory.CreateDbContextAsync(
                        _cancellationToken
                    );

                    // Загружаем Host с TwitchUser
                    var host = await dbContext
                        .Hosts.Include(h => h.TwitchUser)
                        .FirstOrDefaultAsync(h => h.TwitchId == twEvent.UserId, _cancellationToken);

                    if (host is not null)
                    {
                        host.TwitchId = twEvent.UserId;
                        // Обновление TwitchUser выполняется отдельным сервисом

                        if (!host.IsPrivated)
                        {
                            var waifu = await dbContext.Waifus.FindAsync(
                                host.WaifuRollId,
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
                                    waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(
                                        waifu
                                    );

                                    dbContext.Waifus.Update(waifu);
                                    await dbContext.SaveChangesAsync(_cancellationToken);

                                    waifu.IsMerged = true;
                                    waifu.ImageUrl = options.Value.ShikimoriSite + waifu.ImageUrl;

                                    // Проверяем что TwitchUser загружен
                                    if (host.TwitchUser == null)
                                    {
                                        throw new InvalidOperationException(
                                            $"TwitchUser не найден для Host {twEvent.UserId}"
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
                                var spanaa =
                                    DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3))
                                    - host.WhenPrivated;

                                if (spanaa.HasValue)
                                {
                                    var span = spanaa.Value;
                                    var template9 = GetTimeSpanText(span, waifu);
                                    var message9 = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                                        twEvent.UserName,
                                        template9
                                    );
                                    await client.SendMessageToMainTwitchAsync(message9, logger);
                                    return;

                                    static string GetTimeSpanText(TimeSpan span, Waifu waifu)
                                    {
                                        var totalDays = span.Days;

                                        // Рассчитываем годы, месяцы, недели и оставшиеся дни
                                        var years = totalDays / 365;
                                        var remainingDays = totalDays % 365;

                                        var months = remainingDays / 30;
                                        remainingDays %= 30;

                                        var weeks = remainingDays / 7;
                                        remainingDays %= 7;

                                        // Формируем строки только для ненулевых значений
                                        var yearsText =
                                            years > 0
                                                ? $"{years} {GetCorrectForm(years, "год", "года", "лет")}"
                                                : null;
                                        var monthsText =
                                            months > 0
                                                ? $"{months} {GetCorrectForm(months, "месяц", "месяца", "месяцев")}"
                                                : null;
                                        var weeksText =
                                            weeks > 0
                                                ? $"{weeks} {GetCorrectForm(weeks, "неделя", "недели", "недель")}"
                                                : null;
                                        var daysText =
                                            remainingDays > 0
                                                ? $"{remainingDays} {GetCorrectForm(remainingDays, "день", "дня", "дней")}"
                                                : null;

                                        var hours =
                                            span.Hours > 0
                                                ? $"{span.Hours} {GetCorrectForm(span.Hours, "час", "часа", "часов")}"
                                                : null;
                                        var minutes =
                                            span.Minutes > 0
                                                ? $"{span.Minutes} {GetCorrectForm(span.Minutes, "минута", "минуты", "минут")}"
                                                : null;
                                        var seconds =
                                            span.Seconds > 0
                                                ? $"{span.Seconds} {GetCorrectForm(span.Seconds, "секунда", "секунды", "секунд")}"
                                                : null;

                                        // Собираем все части в одну строку, пропуская null
                                        var parts = new[]
                                        {
                                            yearsText,
                                            monthsText,
                                            weeksText,
                                            daysText,
                                            hours,
                                            minutes,
                                            seconds,
                                        }
                                            .Where(part => !string.IsNullOrEmpty(part))
                                            .ToArray();

                                        var charName = waifu.Name;
                                        var title = !string.IsNullOrWhiteSpace(waifu.Anime)
                                            ? " из аниме " + waifu.Anime
                                            : " из манги " + waifu.Manga;
                                        var charText = charName + title;

                                        return $@"{{user}}, ты в браке с {charText} уже {string.Join(", ", parts)}!";
                                    }

                                    // Вспомогательная функция для склонения слов
                                    static string GetCorrectForm(
                                        int number,
                                        string form1,
                                        string form2,
                                        string form5
                                    )
                                    {
                                        number = Math.Abs(number) % 100;
                                        var remainder = number % 10;

                                        return number is > 10 and < 20 ? form5
                                            : remainder is > 1 and < 5 ? form2
                                            : remainder == 1 ? form1
                                            : form5;
                                    }
                                }
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

                    // Гарантируем наличие пользователя в TwitchUsers перед созданием Host
                    await twitchUserEnsureService.EnsureUserExistsAsync(args, _cancellationToken);

                    host = new Host
                    {
                        TwitchId = twEvent.UserId,
                        HostCoolDown = new HostCoolDown { HostId = twEvent.UserId },
                        HostGreetings = new HostAutoHello { HostId = twEvent.UserId },
                    };

                    dbContext.Hosts.Add(host);

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
        }
    }

    public async Task<(Waifu? waifu, Host? host)> Unmerge(string nickname)
    {
        await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);
        var host = await dbContext
            .Hosts.Include(e => e.TwitchUser)
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
                    dbContext.Hosts.Update(host);
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

    public async Task<(Waifu? waifu, Host? host)> Unmerge(int id)
    {
        await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);

        var host = await dbContext.Hosts.SingleOrDefaultAsync(
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
                    dbContext.Hosts.Update(host);
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
    }
}
