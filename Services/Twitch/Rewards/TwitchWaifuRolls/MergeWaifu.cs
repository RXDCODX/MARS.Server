using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;
using TwitchLib.Api.Helix.Models.Chat;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

public class MergeWaifu : BackgroundService
{
    private readonly ITwitchAPI _api;
    private readonly ITwitchClient _client;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly ILogger<MergeWaifu> _logger;
    private readonly WaifuRollService _waifuRollService;
    private readonly TokenService _tokenService;
    private readonly CancellationToken _cancellationToken;
    private readonly WaifuRollDataBaseHelper _waifuDbHelper;
    private readonly IOptions<ShikimoriClientOptions> _options;

    public MergeWaifu(
        ILogger<MergeWaifu> logger,
        ITwitchClient client,
        WaifuRollService waifuRollService,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IDbContextFactory<AppDbContext> factory,
        ITwitchAPI api,
        EventSubService eventSubService,
        TokenService tokenService,
        IHostApplicationLifetime lifetime,
        WaifuRollDataBaseHelper waifuDbHelper,
        IOptions<ShikimoriClientOptions> options
    )
    {
        _logger = logger;
        _client = client;
        _waifuRollService = waifuRollService;
        _hubContext = hubContext;
        _factory = factory;
        _api = api;
        _tokenService = tokenService;
        _waifuDbHelper = waifuDbHelper;
        _options = options;
        _cancellationToken = lifetime.ApplicationStopping;

        lifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                MergeWaifuTwitchEvent;
        });
    }

    public async Task MergeWaifuTwitchEvent(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            if (twEvent.Reward.Cost == 2)
            {
                await using AppDbContext dbContext = await _factory.CreateDbContextAsync(
                    _cancellationToken
                );
                var host = await dbContext.Hosts.FindAsync(twEvent.UserId);
                if (host is not null)
                {
                    host.TwitchId = twEvent.UserId;
                    host.Name = twEvent.UserName;

                    if (!host.IsPrivated)
                    {
                        var waifu = dbContext.Waifus.Find(host.WaifuRollId);
                        if (waifu is { IsPrivated: false })
                        {
                            var isMerged =
                                await _waifuRollService.MergeTheWaifu(host, waifu)
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
                                    waifu = await _waifuDbHelper.EnsureWaifuHaveImageIrl(waifu);

                                    dbContext.Waifus.Update(waifu);
                                }

                                await dbContext.SaveChangesAsync(_cancellationToken);

                                waifu.IsMerged = true;
                                waifu.ImageUrl = _options.Value.ShikimoriSite + waifu.ImageUrl;

                                var color = await _api.Helix.Chat.GetUserChatColorAsync(
                                    [twEvent.UserId]
                                );
                                var avatarUrl = await _api.Helix.Users.GetUsersAsync(
                                    [twEvent.UserId]
                                );
                                await _hubContext.Clients.All.MergeWaifu(
                                    waifu,
                                    twEvent.UserName,
                                    avatarUrl.Users[0]?.ProfileImageUrl,
                                    color.Data[0]?.Color
                                );

                                if (_tokenService.Token != null)
                                {
                                    await _api.Helix.Chat.SendChatAnnouncementAsync(
                                        TwitchExstension.ChannelId,
                                        TwitchExstension.ChannelId,
                                        message,
                                        AnnouncementColors.Primary,
                                        _tokenService.Token.AccessToken
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
                            await _client.SendMessageToMainTwitchAsync(message, _logger);

                            return;
                        }
                        else
                        {
                            var tempLate3 = "@{user}, не удалось найти твою любовь в бд :-(";
                            var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                                twEvent.UserName,
                                tempLate3
                            );
                            await _client.SendMessageToMainTwitchAsync(message, _logger);

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
                                await _client.SendMessageToMainTwitchAsync(message9, _logger);
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

                                    if (number is > 10 and < 20)
                                    {
                                        return form5;
                                    }

                                    if (remainder is > 1 and < 5)
                                    {
                                        return form2;
                                    }

                                    if (remainder == 1)
                                    {
                                        return form1;
                                    }

                                    return form5;
                                }
                            }
                        }

                        var tempLate2 = "@{user}, ты уже помолвен(-а), сорян!";
                        var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                            twEvent.UserName,
                            tempLate2
                        );
                        await _client.SendMessageToMainTwitchAsync(message, _logger);
                        return;
                    }
                }

                host = new()
                {
                    TwitchId = twEvent.UserId,
                    Name = twEvent.UserName,
                    HostCoolDown = new() { HostId = twEvent.UserId },
                    HostGreetings = new() { HostId = twEvent.UserId },
                };

                dbContext.Hosts.Add(host);

                await dbContext.SaveChangesAsync(_cancellationToken);

                var tempLate4 = "@{user}, ты новенький, тебе пока нельзя!";
                var message3 = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                    twEvent.UserName,
                    tempLate4
                );
                await _client.SendMessageToMainTwitchAsync(message3, _logger);
                return;
            }
        }
    }

    public async Task<(Waifu? waifu, Host? host)> Unmerge(string nickname)
    {
        await using var dbContext = await _factory.CreateDbContextAsync(_cancellationToken);
        var host = await dbContext.Hosts.SingleOrDefaultAsync(
            e => e.Name != null && EF.Functions.Like(e.Name, $"%{nickname}%"),
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

    public async Task<(Waifu? waifu, Host? host)> Unmerge(int id)
    {
        await using var dbContext = await _factory.CreateDbContextAsync(_cancellationToken);

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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
