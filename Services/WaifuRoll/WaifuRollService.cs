using MARS.Server.Services.Shikimori.Entitys;
using MARS.Server.Services.Twitch.Rewards;
using MARS.Server.Services.WaifuRoll.helpers;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuRollService : ITelegramusService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<WaifuRollService> _logger;
    private readonly IOptions<ShikimoriClientOptions> _options;
    private readonly WaifuRollDataBaseHelper _waifuDbHelper;
    private readonly IHostEnvironment _environment;

    public WaifuRollService(
        ILogger<WaifuRollService> logger,
        IOptions<ShikimoriClientOptions> options,
        IDbContextFactory<AppDbContext> factory,
        WaifuRollDataBaseHelper waifuDbHelper,
        IHostEnvironment environment
    )
    {
        _logger = logger;
        _options = options;
        _factory = factory;
        _waifuDbHelper = waifuDbHelper;
        _environment = environment;
    }

    private static DateTimeOffset MoscowTime =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTimeOffset.Now.LocalDateTime,
            TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time")
        );

    public async Task<Waifu?> RollTheWaifu(string id, string? displayName = null)
    {
        var pass = false;

        await using AppDbContext dbContext = await _factory.CreateDbContextAsync();
        Host? host = await dbContext.Hosts.FindAsync(id);
        var cd = dbContext.HostsCoolDowns.FirstOrDefault(e => e.HostId == id);
        if (host != null && cd is not null)
        {
            if (cd.HostId == host.TwitchId)
            {
                var isCDed =
                    DateTimeOffset.Now - cd.Time >= TimeSpan.FromHours(1)
                    || _environment.IsDevelopment();
                if (isCDed)
                {
                    pass = true;
                }
            }
            else
            {
                cd.HostId = host.TwitchId;
                cd.Time = DateTimeOffset.Now;

                await dbContext.AddAsync(cd);
                pass = true;
            }
        }
        else
        {
            cd = new HostCoolDown() { HostId = id };

            host = new Host
            {
                TwitchId = id,
                Name = displayName,
                HostGreetings = new HostAutoHello() { HostId = id },
                HostCoolDown = cd,
            };

            await dbContext.AddAsync(host);

            pass = true;
        }

        await dbContext.SaveChangesAsync();

        if (pass)
        {
            // Находим вайфу с минимальным LastOrder за один SQL-запрос
            var waifu = await dbContext
                .Waifus.Where(e => !e.IsPrivated) // Учитываем только публичные вайфу
                .OrderBy(e => e.LastOrder) // Сортируем по LastOrder
                .FirstOrDefaultAsync(); // Берем первую вайфу

            if (waifu != null)
            {
                host.WaifuRollId = waifu.ShikiId;
                host.WhenOrdered = DateTimeOffset.Now;
                host.OrderCount++;

                waifu.LastOrder = DateTimeOffset.Now;
                waifu.OrderCount++;

                if (string.IsNullOrWhiteSpace(waifu.ImageUrl))
                {
                    waifu = await _waifuDbHelper.EnsureWaifuHaveImageIrl(waifu);

                    dbContext.Waifus.Update(waifu);
                }

                cd.Time = DateTimeOffset.Now;
                await dbContext.SaveChangesAsync();

                waifu.ImageUrl = _options.Value.ShikimoriSite + waifu.ImageUrl;

                return waifu;
            }
        }

        return null;
    }

    public async Task<(Waifu?, bool)> AddNewWaifu(ShikiCharacter character)
    {
        try
        {
            await using AppDbContext dbContext = await _factory.CreateDbContextAsync();

            if (dbContext.Waifus.Any(e => e.ShikiId == character.id.ToString()))
            {
                return (null, false);
            }

            var waifu = character.CreateWaifu();
            waifu = await _waifuDbHelper.EnsureWaifuHaveImageIrl(waifu);
            await dbContext.Waifus.AddAsync(waifu);
            await dbContext.SaveChangesAsync();
            return (waifu, false);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return (null, true);
        }
    }

    public async Task<bool> MergeTheWaifu(Host host, Waifu waifu, bool makeprivate = true)
    {
        await using AppDbContext dbContext = await _factory.CreateDbContextAsync();

        if (makeprivate)
        {
            waifu.IsPrivated = true;
            host.IsPrivated = true;
            host.WaifuBrideId = waifu.ShikiId;
            host.WhenPrivated = MoscowTime;
        }
        else
        {
            waifu.IsPrivated = false;
            host.IsPrivated = false;
        }

        return await dbContext.SaveChangesAsync() != 0;
    }

    public async Task<string?> AutoHello(string id, string displayName)
    {
        await using AppDbContext dbContext = await _factory.CreateDbContextAsync();

        var host = dbContext.Hosts.Find(id);

        if (host?.IsPrivated ?? false)
        {
            var isChecked = false;

            HostAutoHello? greet = dbContext.HostsGreetings.FirstOrDefault(e => e.HostId == id);
            if (greet is null)
            {
                isChecked = true;

                greet = new HostAutoHello { HostId = id, Time = DateTimeOffset.Now };

                dbContext.Add(greet);

                await dbContext.SaveChangesAsync();
            }
            else if (greet.Time <= DateTimeOffset.Now.AddHours(-20))
            {
                isChecked = true;
            }

            if (isChecked)
            {
                if (host.WaifuBrideId != null)
                {
                    Waifu? waifu = await dbContext.Waifus.FindAsync(host.WaifuBrideId);
                    var helloMsg = await GetHelloText();
                    var fixedmsg = await ConvertFixLinksInHelloMessages(helloMsg);

                    HostAutoHello? hello = dbContext.HostsGreetings.FirstOrDefault(e =>
                        e.HostId == id
                    );

                    if (hello != default)
                    {
                        hello.Time = DateTimeOffset.Now;
                    }
                    else
                    {
                        hello = new HostAutoHello { HostId = id, Time = DateTimeOffset.Now };

                        dbContext.Add(hello);
                    }

                    await dbContext.SaveChangesAsync();

                    var message = string.Concat(
                        "@{user}, твой супруг, {waifuName} , оставил(-а) тебе сообщение: \"",
                        fixedmsg,
                        " \""
                    );
                    message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        displayName,
                        message,
                        waifu: waifu
                    );

                    return message;
                }
            }
        }
        else if (host == default)
        {
            host = new Host
            {
                TwitchId = id,
                Name = displayName,
                HostCoolDown = new HostCoolDown() { HostId = id },
                HostGreetings = new HostAutoHello() { HostId = id },
            };

            await dbContext.AddAsync(host);
            await dbContext.SaveChangesAsync();
        }

        return null;
    }

    private async Task<string> ConvertFixLinksInHelloMessages(string message)
    {
        if (message.Contains("{randomHost}"))
        {
            await using AppDbContext dbContext = await _factory.CreateDbContextAsync();
            var count = dbContext.Hosts.Count(e => !e.IsPrivated);

            if (count > 0)
            {
                Host host = await dbContext
                    .Hosts.AsNoTracking()
                    .Include(e => e.HostCoolDown)
                    .Include(e => e.HostGreetings)
                    .Where(e => !e.IsPrivated)
                    .ElementAtAsync(Random.Shared.Next(count));

                var replace = message.Replace("{randomHost}", host.Name);
                var finalMessage = string.Concat(
                    "@{user}, твой супруг прислал(-а) тебе сообщение: ",
                    replace
                );
                return finalMessage;
            }
        }

        return message;
    }

    private ValueTask<string> GetHelloText()
    {
        var lines = File.ReadAllLines(
            Path.Combine(Directory.GetCurrentDirectory(), "AutoHelloMessages.txt")
        );
        var index = Random.Shared.Next(lines.Length);

        return ValueTask.FromResult(lines[index]);
    }
}
