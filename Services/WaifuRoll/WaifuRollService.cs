using MARS.Server.Services.Twitch.Rewards;
using MARS.Server.Services.WaifuRoll.helpers;
using MARS.Server.Services.WaifuRoll.Interfaces;
using MARS.Server.Services.WaifuRoll.Models;
using ShikimoriSharp.Classes;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuRollService(
    IOptions<ShikimoriClientOptions> options,
    IDbContextFactory<AppDbContext> factory,
    WaifuRollEnsurenceService waifuDbHelper
) : BackgroundService, IWaifuRollService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }

    public async Task<Waifu?> RollTheWaifu(
        string id,
        string? displayName = null,
        bool forcePass = false
    )
    {
        Waifu? result = null;

        if (!string.IsNullOrWhiteSpace(id))
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
                    cd = new HostCoolDown { HostId = id };

                    await dbContext.HostsCoolDowns.AddAsync(cd);

                    pass = true;
                }
            }
            else
            {
                cd = new HostCoolDown { HostId = id };

                host = new Host
                {
                    TwitchId = id,
                    HostGreetings = new HostAutoHello { HostId = id },
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
                var waifu = await dbContext
                    .Waifus.Where(e => !e.IsPrivated)
                    .OrderBy(e => e.LastOrder)
                    .FirstOrDefaultAsync();

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
                    }

                    // Убеждаемся, что поля аниме и манги заполнены
                    waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(waifu);
                    dbContext.Waifus.Update(waifu);

                    cd.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                    await dbContext.SaveChangesAsync();

                    waifu.ImageUrl = options.Value.ShikimoriSite + waifu.ImageUrl;

                    result = waifu;
                }
            }
        }

        return result;
    }

    public async Task<OperationResult<TelegramRollWaifuResponse>> TelegramRollWaifu(string name)
    {
        var result = OperationResult<TelegramRollWaifuResponse>.Bad(
            "Ошибка при ролле вайфу через Telegram"
        );

        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                await using var dbContext = await factory.CreateDbContextAsync();

                var host = await dbContext.Hosts
                    .Include(e => e.TwitchUser)
                    .FirstOrDefaultAsync(e =>
                        e.TwitchUser != null && EF.Functions.Like(e.TwitchUser.DisplayName, $"%{name}%")
                    );

                if (host is not null)
                {
                    var waifu = await RollTheWaifu(host.TwitchId, host.TwitchUser?.DisplayName ?? string.Empty, true);

                    var response = new TelegramRollWaifuResponse
                    {
                        Waifu = waifu,
                        Host = host,
                        Husband = null,
                    };

                    if (waifu is { IsPrivated: true })
                    {
                        var husband = await dbContext.Hosts.FirstAsync(e =>
                            e.WaifuBrideId == waifu.ShikiId
                        );
                        response.Husband = husband;
                    }

                    result = OperationResult<TelegramRollWaifuResponse>.Ok(
                        "Вайфу успешно выпала",
                        response
                    );
                }
                else
                {
                    result = OperationResult<TelegramRollWaifuResponse>.Bad("Хост не найден");
                }
            }
            catch (Exception ex)
            {
                result = OperationResult<TelegramRollWaifuResponse>.Bad(
                    $"Ошибка при ролле вайфу: {ex.Message}"
                );
            }
        }
        else
        {
            result = OperationResult<TelegramRollWaifuResponse>.Bad(
                "Имя хоста не может быть пустым"
            );
        }

        return result;
    }

    public async Task<OperationResult<AddNewWaifuResponse>> AddNewWaifu(FullCharacter? character)
    {
        var result = OperationResult<AddNewWaifuResponse>.Bad("Ошибка при добавлении новой вайфу");

        if (character != null)
        {
            try
            {
                await using AppDbContext dbContext = await factory.CreateDbContextAsync();

                if (dbContext.Waifus.Any(e => e.ShikiId == character.Id.ToString()))
                {
                    result = OperationResult<AddNewWaifuResponse>.Bad(
                        "Вайфу уже существует в базе данных"
                    );
                }
                else
                {
                    var waifu = new Waifu
                    {
                        ShikiId = character.Id.ToString(),
                        Name = character.Name ?? character.Russian ?? "Unknown",
                        ImageUrl = character.Image?.Original ?? string.Empty,
                        WhenAdded = DateTimeOffset.Now,
                        LastOrder = DateTimeOffset.Now,
                        OrderCount = 0,
                        IsPrivated = false,
                        Manga = character.Mangas.MinBy(e => e.Russian.Length)?.Russian,
                        Anime = character.Animes.MinBy(e => e.Russian.Length)?.Russian,
                    };

                    waifu = await waifuDbHelper.EnsureWaifuHaveImageIrl(waifu);
                    waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(waifu);

                    await dbContext.Waifus.AddAsync(waifu);
                    await dbContext.SaveChangesAsync();

                    var response = new AddNewWaifuResponse { Waifu = waifu, HasError = false };

                    result = OperationResult<AddNewWaifuResponse>.Ok(
                        "Вайфу успешно добавлена",
                        response
                    );
                }
            }
            catch (Exception ex)
            {
                var response = new AddNewWaifuResponse { Waifu = null, HasError = true };

                result = OperationResult<AddNewWaifuResponse>.Bad(
                    $"Ошибка при добавлении вайфу: {ex.Message}",
                    response
                );
            }
        }
        else
        {
            result = OperationResult<AddNewWaifuResponse>.Bad("Персонаж не может быть null");
        }

        return result;
    }

    public async Task<bool> MergeTheWaifu(Host? host, Waifu? waifu, bool makeprivate = true)
    {
        var result = false;

        if (host != null && waifu != null)
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

            result = await dbContext.SaveChangesAsync() != 0;
        }

        return result;
    }

    public async Task<string?> AutoHello(string id, string displayName)
    {
        string? result = null;

        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(displayName))
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
                            hello.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                        }
                        else
                        {
                            hello = new HostAutoHello
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

                        result = message;
                    }
                }
            }
            else if (host == default)
            {
                host = new Host
                {
                    TwitchId = id,
                    HostCoolDown = new HostCoolDown { HostId = id },
                    HostGreetings = new HostAutoHello { HostId = id },
                };

                await dbContext.AddAsync(host);
                await dbContext.SaveChangesAsync();
            }
        }

        return result;
    }

    private async Task<string> ConvertFixLinksInHelloMessages(string message)
    {
        var result = message;

        if (!string.IsNullOrWhiteSpace(message) && message.Contains("{randomHost}"))
        {
            await using AppDbContext dbContext = await factory.CreateDbContextAsync();
            var count = dbContext.Hosts.Count(e => !e.IsPrivated);

            if (count > 0)
            {
                Host host = await dbContext
                    .Hosts.AsNoTracking()
                    .Include(e => e.HostCoolDown)
                    .Include(e => e.HostGreetings)
                    .Include(e => e.TwitchUser)
                    .Where(e => !e.IsPrivated)
                    .ElementAtAsync(Random.Shared.Next(count));

                var replace = message.Replace("{randomHost}", host.TwitchUser?.DisplayName ?? "Unknown");
                result = string.Concat(
                    "@{user}, твой супруг прислал(-а) тебе сообщение: ",
                    replace
                );
            }
        }

        return result;
    }

    private static ValueTask<string> GetHelloText()
    {
        var lines = File.ReadAllLines(
            Path.Combine(Directory.GetCurrentDirectory(), "AutoHelloMessages.txt")
        );
        var index = Random.Shared.Next(lines.Length);

        return ValueTask.FromResult(lines[index]);
    }
}
