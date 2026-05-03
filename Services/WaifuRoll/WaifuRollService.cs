using System.Collections.Concurrent;
using MARS.Server.ApplicationState;
using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.Rewards;
using MARS.Server.Services.Twitch.WeddingAnniversary;
using MARS.Server.Services.WaifuRoll.helpers;
using MARS.Server.Services.WaifuRoll.Interfaces;
using MARS.Server.Services.WaifuRoll.Models;
using ShikimoriSharp.Classes;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuRollService(
    IOptions<ShikimoriClientOptions> options,
    IDbContextFactory<AppDbContext> factory,
    WaifuRollEnsurenceService waifuDbHelper,
    TwitchUserEnsureService twitchUserEnsureService,
    WeddingAnniversaryService anniversaryService
) : BackgroundService, IWaifuRollService
{
    /// <summary>
    /// Обертка семафора с подсчетом использований
    /// </summary>
    private class SemaphoreWrapper
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int UseCount = 0;
    }

    /// <summary>
    /// Словарь семафоров для синхронизации операций по TwitchId
    /// Предотвращает race condition при создании Host
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreWrapper> _hostSemaphores = new();

    /// <summary>
    /// Получить или создать семафор для конкретного TwitchId
    /// </summary>
    private SemaphoreSlim GetOrCreateSemaphore(string twitchId)
    {
        var wrapper = _hostSemaphores.GetOrAdd(twitchId, _ => new SemaphoreWrapper());
        Interlocked.Increment(ref wrapper.UseCount);
        return wrapper.Semaphore;
    }

    /// <summary>
    /// Освободить семафор после использования и удалить если больше не используется
    /// </summary>
    private void ReleaseSemaphore(string twitchId, SemaphoreSlim semaphore)
    {
        semaphore.Release();

        if (_hostSemaphores.TryGetValue(twitchId, out var wrapper))
        {
            var count = Interlocked.Decrement(ref wrapper.UseCount);

            // Если семафор больше не используется - удаляем и диспозим
            if (count == 0)
            {
                if (_hostSemaphores.TryRemove(twitchId, out var removedWrapper))
                {
                    removedWrapper.Semaphore.Dispose();
                }
            }
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
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

    public async Task<Waifu?> RollTheWaifu(
        string id,
        string? displayName = null,
        bool forcePass = false
    )
    {
        Waifu? result = null;

        if (!string.IsNullOrWhiteSpace(id))
        {
            var semaphore = GetOrCreateSemaphore(id);
            await semaphore.WaitAsync();

            try
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

                            var cdFromEnv = await GetWaifuRollCoolDownAsync();

                            var isCDed = now - cdTime >= cdFromEnv;
                            if (isCDed)
                            {
                                pass = true;
                            }
                        }
                        else
                        {
                            cd.HostId = host.TwitchId;
                            cd.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));

                            dbContext.HostsCoolDowns.Update(cd);
                            pass = true;
                        }
                    }
                    else
                    {
                        cd = new HostCoolDown { HostId = id };
                        host.HostCoolDown = cd;

                        dbContext.Hosts.Update(host);

                        pass = true;
                    }
                }
                else
                {
                    // Гарантируем наличие пользователя в TwitchUsers перед созданием Host
                    if (twitchUserEnsureService is not null)
                    {
                        await twitchUserEnsureService.EnsureUserExistsAsync(id);
                    }

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

                        var shouldIncrementGuarantee =
                            !forcePass
                            && !string.Equals(
                                id,
                                TwitchExstension.ChannelId,
                                StringComparison.OrdinalIgnoreCase
                            );

                        if (shouldIncrementGuarantee)
                        {
                            host.OrderCount++;
                            waifu.OrderCount++;
                        }
                        else
                        {
                            host.OrderCount = host.OrderCount;
                            waifu.OrderCount = waifu.OrderCount;
                        }

                        host.HostCoolDown.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));

                        waifu.LastOrder = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));

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
            finally
            {
                ReleaseSemaphore(id, semaphore);
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

                var host = await dbContext
                    .Hosts.Include(e => e.TwitchUser)
                    .FirstOrDefaultAsync(e =>
                        e.TwitchUser != null
                        && EF.Functions.Like(e.TwitchUser.DisplayName, $"%{name}%")
                    );

                if (host is not null)
                {
                    var waifu = await RollTheWaifu(
                        host.TwitchId,
                        host.TwitchUser?.DisplayName ?? string.Empty,
                        true
                    );

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

                    if (
                        response.Husband is null
                        && host.IsPrivated
                        && !string.IsNullOrWhiteSpace(host.WaifuBrideId)
                    )
                    {
                        response.Husband = await dbContext.Hosts.FirstOrDefaultAsync(e =>
                            e.WaifuBrideId == host.WaifuBrideId
                        );
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
        var result = OperationResult<AddNewWaifuResponse>.Bad(
            "ошибка при добавлении нового персонажа"
        );

        if (character != null)
        {
            try
            {
                await using AppDbContext dbContext = await factory.CreateDbContextAsync();

                if (dbContext.Waifus.Any(e => e.ShikiId == character.Id.ToString()))
                {
                    result = OperationResult<AddNewWaifuResponse>.Bad(
                        "персонаж уже существует в базе данных"
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
                        "вайфу успешно добавлена",
                        response
                    );
                }
            }
            catch (Exception ex)
            {
                var response = new AddNewWaifuResponse { Waifu = null, HasError = true };

                result = OperationResult<AddNewWaifuResponse>.Bad(
                    $"ошибка при добавлении вайфу: {ex.Message}",
                    response
                );
            }
        }
        else
        {
            result = OperationResult<AddNewWaifuResponse>.Bad("персонаж не может быть null");
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
            var semaphore = GetOrCreateSemaphore(id);
            await semaphore.WaitAsync();

            try
            {
                await using AppDbContext dbContext = await factory.CreateDbContextAsync();

                var host = await dbContext
                    .Hosts.Include(e => e.HostGreetings)
                    .Include(e => e.HostCoolDown)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.TwitchId == id);

                if (host is { IsPrivated: true })
                {
                    var isChecked = false;
                    var greet = host.HostGreetings;

                    if (greet.Time <= DateTimeOffset.Now.AddHours(-20))
                    {
                        isChecked = true;
                    }

                    if (isChecked)
                    {
                        // Проверяем наличие непоздравленной годовщины
                        var anniversary = await anniversaryService.GetNextUnsentAnniversaryAsync(
                            id
                        );

                        if (anniversary.HasValue && host.WaifuBrideId != null)
                        {
                            // Если есть годовщина - отправляем поздравление от супруга
                            Waifu? waifu = await dbContext.Waifus.FindAsync(host.WaifuBrideId);
                            var spouseName = waifu?.Name ?? "супруг(а)";

                            var message = WeddingAnniversaryService.BuildCongratulationMessageFromSpouse(
                                displayName,
                                spouseName,
                                anniversary.Value
                            );

                            // Отмечаем годовщину как отправленную
                            await anniversaryService.MarkAnniversaryAsSentAsync(
                                id,
                                anniversary.Value.Months
                            );

                            // Обновляем время последнего приветствия
                            greet.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                            dbContext.HostsGreetings.Update(greet);
                            await dbContext.SaveChangesAsync();

                            result = message;
                        }
                        else if (host.WaifuBrideId != null)
                        {
                            // Если годовщины нет - отправляем обычное AutoHello сообщение
                            Waifu? waifu = await dbContext.Waifus.FindAsync(host.WaifuBrideId);
                            var helloMsg = await GetHelloText();
                            var fixedmsg = await ConvertFixLinksInHelloMessages(helloMsg);

                            greet.Time = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                            dbContext.HostsGreetings.Update(greet);

                            await dbContext.SaveChangesAsync();

                            var spouseName = waifu?.Name ?? "супруг(а)";
                            var message =
                                $"@{{user}}, твой супруг {spouseName} прислал(а) тебе сообщение: \"{fixedmsg}\"";
                            message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                                displayName,
                                message,
                                waifu: waifu
                            );

                            result = message;
                        }
                    }
                }
                else if (host == null)
                {
                    // Гарантируем наличие пользователя в TwitchUsers перед созданием Host
                    await twitchUserEnsureService.EnsureUserExistsAsync(id);

                    host = new Host
                    {
                        TwitchId = id,
                        HostCoolDown = new HostCoolDown { HostId = id },
                        HostGreetings = new HostAutoHello { HostId = id },
                    };

                    host.HostCoolDown.Host = null;
                    host.HostGreetings.Host = null;

                    await dbContext.AddAsync(host);
                    await dbContext.SaveChangesAsync();
                }
            }
            finally
            {
                ReleaseSemaphore(id, semaphore);
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

                var replace = message.Replace(
                    "{randomHost}",
                    host.TwitchUser?.DisplayName ?? "Unknown"
                );
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

    public async Task<TimeSpan> GetWaifuRollCoolDownAsync()
    {
        await using var dbContext = await factory.CreateDbContextAsync();

        var cooldownValue = await dbContext
            .RootState.AsNoTracking()
            .Where(e => e.Name == RootStateKeys.WaifuRollCooldownMinutes)
            .Select(e => e.Value)
            .FirstOrDefaultAsync();

        var cooldownMinutes =
            long.TryParse(cooldownValue, out var parsedCooldownMinutes) && parsedCooldownMinutes > 0
                ? parsedCooldownMinutes
                : 20;

        return TimeSpan.FromMinutes(cooldownMinutes);
    }
}
