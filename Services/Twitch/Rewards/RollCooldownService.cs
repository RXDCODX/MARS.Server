using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MARS.Server.Services.Twitch.Rewards;

public class RollCooldownService(IDbContextFactory<AppDbContext> factory)
{
    private const int MaxRetries = 2;

    public async Task<(bool allowed, TimeSpan remaining)> CheckAndUpdateCooldownAsync(
        string twitchUserId,
        string rollType,
        TimeSpan cooldown
    )
    {
        var result = (allowed: true, remaining: TimeSpan.Zero);

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using var dbContext = await factory.CreateDbContextAsync();

                var existing = await dbContext
                    .RollCooldowns.AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        r.TwitchUserId == twitchUserId && r.RollType == rollType
                    );

                if (existing is not null)
                {
                    var elapsed = DateTime.Now - existing.LastRollTime;
                    if (elapsed < cooldown)
                    {
                        result = (false, cooldown - elapsed);
                        return result;
                    }

                    var tracked = await dbContext.RollCooldowns.FirstAsync(r =>
                        r.TwitchUserId == twitchUserId && r.RollType == rollType
                    );
                    tracked.LastRollTime = DateTime.Now;
                }
                else
                {
                    dbContext.RollCooldowns.Add(
                        new RollCooldown
                        {
                            TwitchUserId = twitchUserId,
                            RollType = rollType,
                            LastRollTime = DateTime.Now,
                        }
                    );
                }

                await dbContext.SaveChangesAsync();
                result = (true, TimeSpan.Zero);
                return result;
            }
            catch (Exception ex) when (attempt < MaxRetries && IsDuplicateKey(ex))
            {
                // Duplicate key — concurrent insert, retry
            }
        }

        return result;
    }

    private static bool IsDuplicateKey(Exception ex)
    {
        if (ex is PostgresException postgres && postgres.SqlState == "23505")
        {
            return true;
        }

        if (ex is DbUpdateException dbUpdate && dbUpdate.InnerException is PostgresException inner)
        {
            return inner.SqlState == "23505";
        }

        return false;
    }
}
