using MARS.Server.Services.Shikimori.Entitys;
using MARS.Server.Services.Twitch.Rewards;
using MARS.Server.Services.WaifuRoll.helpers;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuRollService(
    ILogger<WaifuRollService> logger,
    IOptions<ShikimoriClientOptions> options,
    IDbContextFactory<AppDbContext> factory,
    WaifuRollDataBaseHelper waifuDbHelper
) : ITelegramusService
{
    public async Task<Waifu?> RollTheWaifu(
        string id,
        string? displayName = null,
        bool forcePass = false
    )
    {
        var pass = false;

        await using AppDbContext dbContext = await factory.CreateDbContextAsync();
        var host = dbContext
            .Hosts.Include(e => e.HostCoolDown)
            .FirstOrDefault(e => e.TwitchId == id);
        var cd = host?.HostCoolDown;
        if (host != null)
        {
            if (cd is not null)
            {
                if (cd.HostId == host.TwitchId)
                {
                    var now = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                    var cdTime = cd.Time.ToOffset(TimeSpan.FromHours(3));

                    var isCDed = now - cdTime >= TimeSpan.FromHours(1);
                    if (isCDed)
                    {
                        pass = true;
                    }
                }
                else
                {
                    cd.HostId = host.TwitchId;
                    cd.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));

                    await dbContext.AddAsync(cd);
                    pass = true;
                }
            }
            else
            {
                cd = new() { HostId = id };

                await dbContext.HostsCoolDowns.AddAsync(cd);

                pass = true;
            }
        }
        else
        {
            cd = new() { HostId = id };

            host = new()
            {
                TwitchId = id,
                Name = displayName,
                HostGreetings = new() { HostId = id },
                HostCoolDown = cd,
            };

            await dbContext.Hosts.AddAsync(host);

            pass = true;
        }

        await dbContext.SaveChangesAsync();

        if (id == TwitchExstension.ChannelId || forcePass)
        {
            pass = true;
        }

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
                host.WhenOrdered = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                host.OrderCount++;
                host.HostCoolDown.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));

                waifu.LastOrder = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                waifu.OrderCount++;

                if (string.IsNullOrWhiteSpace(waifu.ImageUrl))
                {
                    waifu = await waifuDbHelper.EnsureWaifuHaveImageIrl(waifu);

                    dbContext.Waifus.Update(waifu);
                }

                cd.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                await dbContext.SaveChangesAsync();

                waifu.ImageUrl = options.Value.ShikimoriSite + waifu.ImageUrl;

                return waifu;
            }
        }

        return null;
    }

    public async Task<(Waifu? waifu, Host? host)> TelegramRollWaifu(string name)
    {
        await using var dbContext = await factory.CreateDbContextAsync();

        var host = await dbContext.Hosts.FirstOrDefaultAsync(e =>
            e.Name != null && EF.Functions.Like(e.Name, $"%{name}%")
        );

        if (host is not null)
        {
            var waifu = await RollTheWaifu(host.TwitchId, host.Name, true);
            return (waifu, host);
        }

        return (null, null);
    }

    public async Task<(Waifu?, bool)> AddNewWaifu(ShikiCharacter character)
    {
        try
        {
            await using AppDbContext dbContext = await factory.CreateDbContextAsync();

            if (dbContext.Waifus.Any(e => e.ShikiId == character.id.ToString()))
            {
                return (null, false);
            }

            var waifu = character.CreateWaifu();
            waifu = await waifuDbHelper.EnsureWaifuHaveImageIrl(waifu);
            await dbContext.Waifus.AddAsync(waifu);
            await dbContext.SaveChangesAsync();
            return (waifu, false);
        }
        catch (Exception e)
        {
            logger.LogException(e);
            return (null, true);
        }
    }

    public async Task<bool> MergeTheWaifu(Host host, Waifu waifu, bool makeprivate = true)
    {
        await using AppDbContext dbContext = await factory.CreateDbContextAsync();

        if (makeprivate)
        {
            waifu.IsPrivated = true;
            host.IsPrivated = true;
            host.WaifuBrideId = waifu.ShikiId;
            host.WhenPrivated = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
        }
        else
        {
            waifu.IsPrivated = false;
            host.IsPrivated = false;
        }

        dbContext.Waifus.Update(waifu);
        dbContext.Hosts.Update(host);

        return await dbContext.SaveChangesAsync() != 0;
    }

    public async Task<string?> AutoHello(string id, string displayName)
    {
        await using AppDbContext dbContext = await factory.CreateDbContextAsync();

        var host = dbContext.Hosts.Find(id);

        if (host?.IsPrivated ?? false)
        {
            var isChecked = false;

            HostAutoHello? greet = dbContext.HostsGreetings.FirstOrDefault(e => e.HostId == id);
            if (greet is null)
            {
                isChecked = true;

                greet = new() { HostId = id, Time = DateTimeOffset.Now };

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
                        hello.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                    }
                    else
                    {
                        hello = new()
                        {
                            HostId = id,
                            Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3)),
                        };

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
            host = new()
            {
                TwitchId = id,
                Name = displayName,
                HostCoolDown = new() { HostId = id },
                HostGreetings = new() { HostId = id },
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
            await using AppDbContext dbContext = await factory.CreateDbContextAsync();
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
