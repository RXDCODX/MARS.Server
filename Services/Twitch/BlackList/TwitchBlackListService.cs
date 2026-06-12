using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Interfaces;

namespace MARS.Server.Services.Twitch.BlackList;

public class TwitchBlackListService(
    IDbContextFactory<AppDbContext> factory,
    ILogger<TwitchBlackListService> logger,
    ITwitchAPI api,
    ITwitchUserEnsureService ensureService,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private readonly Lock _locker = new();

    public async Task<TwitchUser?> AddTwitchBlacklistedUserAsync(
        string? input,
        bool markUserBlackListed = true,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        try
        {
            var isTwitchId = long.TryParse(input, out _);

            TwitchUser? user;

            if (isTwitchId)
            {
                user = await ensureService.EnsureUserExistsAsync(input, cancellationToken);
            }
            else
            {
                input = input.StartsWith("@") ? input[1..] : input;

                var apiuser = await api.Helix.Users.GetUsersAsync([], [input]);

                if (apiuser is not { Users: { Length: > 0 } })
                {
                    return null;
                }

                user = await ensureService.EnsureUserExistsAsync(
                    TwitchUser.FromApiUser(apiuser.Users.First())!,
                    cancellationToken
                );
            }

            await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
            user = await dbContext.TwitchUsers.FindAsync([user.TwitchId], cancellationToken);

            if (user is not null)
            {
                user.IsInBlockList = markUserBlackListed;
                dbContext.TwitchUsers.Update(user);
                await dbContext.SaveChangesAsync(cancellationToken);

                await UpdateTwitchBlacklist(cancellationToken);
            }

            return user;
        }
        catch (Exception e)
        {
            logger.LogException(e);
        }

        return null;
    }

    public Task<TwitchUser?> RemoveTwitchBlacklistedUserAsync(
        string? input,
        CancellationToken cancellationToken
    )
    {
        return AddTwitchBlacklistedUserAsync(input, false, cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(async void () =>
        {
            try
            {
                await UpdateTwitchBlacklist(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogException(e);
            }
        });

        return Task.CompletedTask;
    }

    private async Task UpdateTwitchBlacklist(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

        var result = dbContext.TwitchUsers.AsNoTracking().Where(e => e.IsInBlockList).ToList();

        lock (_locker)
        {
            TwitchExstension.BlackList = new ConcurrentBag<TwitchUser>(result);
        }
    }
}
