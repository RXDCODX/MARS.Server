using Hangfire;
using Telegram.Bot.Exceptions;

namespace MARS.Server.Services.Honkai;

public class DailyMarkMarkNotificationsSerivce(
    ITelegramBotClient client,
    ILogger<DailyMarkMarkNotificationsSerivce> logger,
    IWebHostEnvironment environment,
    IDbContextFactory<AppDbContext> factory
) : ITelegramusService
{
    private readonly ILogger _logger = logger;

    public async Task<bool> Add(TelegramUser player)
    {
        player.HonkaiNotifications = true;
        await using var dbContext = await factory.CreateDbContextAsync();
        if (dbContext.TelegramUsers.Any(e => e.UserId == player.UserId))
        {
            dbContext.TelegramUsers.Update(player);
        }
        else
        {
            await dbContext.TelegramUsers.AddAsync(player);
        }

        return await dbContext.SaveChangesAsync() != 0;
    }

    [AutomaticRetry(OnlyOn = [typeof(ApiRequestException)], Attempts = 5)]
    public async Task NotifyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync(stoppingToken);

            if (dbContext.TelegramUsers.Any())
            {
                var copy = await dbContext
                    .TelegramUsers.Where(e => e.HonkaiNotifications || e.ZenlessZoneZeroDailyNotif)
                    .ToListAsync(cancellationToken: stoppingToken);

                foreach (var user in copy)
                    if (
                        DateTimeOffset.Now.Date > user.LastTimeMessage.Date
                        && DateTimeOffset.Now.Hour >= 6
                    )
                    {
                        string message;

                        // Проверяем все возможные комбинации флагов
                        if (
                            user is
                            {
                                HonkaiNotifications: true,
                                ZenlessZoneZeroDailyNotif: true,
                                GenshinImpactDailyNotif: true
                            }
                        )
                        {
                            const string query3 = """
                                Время дневной отметки в Honkai:Star Rail, Zenless Zone Zero и Genshin Impact!

                                Honkai:Star Rail: https://act.hoyolab.com/bbs/event/signin/hkrpg/index.html?act_id=e202303301540311

                                Zenless Zone Zero: https://act.hoyolab.com/bbs/event/signin/zzz/e202406031448091.html?act_id=e202406031448091

                                Genshin Impact: https://act.hoyolab.com/ys/event/signin-sea-v3/index.html?act_id=e202102251931481&lang=ru-ru
                                """;
                            message = query3;
                        }
                        else if (
                            user is { HonkaiNotifications: true, ZenlessZoneZeroDailyNotif: true }
                        )
                        {
                            const string query3 = """
                                Время дневной отметки в Honkai:Star Rail и Zenless Zone Zero!

                                Honkai:Star Rail: https://act.hoyolab.com/bbs/event/signin/hkrpg/index.html?act_id=e202303301540311

                                Zenless Zone Zero: https://act.hoyolab.com/bbs/event/signin/zzz/e202406031448091.html?act_id=e202406031448091
                                """;
                            message = query3;
                        }
                        else if (
                            user is { HonkaiNotifications: true, GenshinImpactDailyNotif: true }
                        )
                        {
                            const string query3 = """
                                Время дневной отметки в Honkai:Star Rail и Genshin Impact!

                                Honkai:Star Rail: https://act.hoyolab.com/bbs/event/signin/hkrpg/index.html?act_id=e202303301540311

                                Genshin Impact: https://act.hoyolab.com/ys/event/signin-sea-v3/index.html?act_id=e202102251931481&lang=ru-ru
                                """;
                            message = query3;
                        }
                        else if (
                            user is
                            { ZenlessZoneZeroDailyNotif: true, GenshinImpactDailyNotif: true }
                        )
                        {
                            const string query3 = """
                                Время дневной отметки в Zenless Zone Zero и Genshin Impact!

                                Zenless Zone Zero: https://act.hoyolab.com/bbs/event/signin/zzz/e202406031448091.html?act_id=e202406031448091

                                Genshin Impact: https://act.hoyolab.com/ys/event/signin-sea-v3/index.html?act_id=e202102251931481&lang=ru-ru
                                """;
                            message = query3;
                        }
                        else if (user.HonkaiNotifications)
                        {
                            const string query = """
                                Время дневной отметки в Honkai:Star Rail!

                                https://act.hoyolab.com/bbs/event/signin/hkrpg/index.html?act_id=e202303301540311
                                """;
                            message = query;
                        }
                        else if (user.ZenlessZoneZeroDailyNotif)
                        {
                            const string query2 = """
                                Время дневной отметки в Zenless Zone Zero!

                                https://act.hoyolab.com/bbs/event/signin/zzz/e202406031448091.html?act_id=e202406031448091
                                """;
                            message = query2;
                        }
                        else if (user.GenshinImpactDailyNotif)
                        {
                            const string query2 = """
                                Время дневной отметки в Genshin Impact!

                                https://act.hoyolab.com/ys/event/signin-sea-v3/index.html?act_id=e202102251931481&lang=ru-ru
                                """;
                            message = query2;
                        }
                        else
                        {
                            message = "Нет активных уведомлений.";
                        }

                        try
                        {
                            await client.SendMessage(
                                user.UserId,
                                message,
                                cancellationToken: stoppingToken
                            );
                        }
                        catch (Exception e)
                            when (e is ApiRequestException && e.Message.Contains("chat not found"))
                        {
                            if (environment.IsProduction())
                            {
                                dbContext.TelegramUsers.Remove(user);
                            }
                        }

                        user.LastTimeMessage = DateTimeOffset.Now;
                    }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Ошибка сервиса напоминаний хонкай");
        }
    }
}
