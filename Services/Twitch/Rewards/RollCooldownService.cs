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
                        return (false, cooldown - elapsed);
                    }

                    existing.LastRollTime = DateTime.Now;
                    dbContext.RollCooldowns.Update(existing);
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
                return (true, TimeSpan.Zero);
            }
            catch (PostgresException ex) when (ex.SqlState == "23505" && attempt < MaxRetries)
            {
                // Duplicate key — concurrent insert, retry
            }
            catch
            {
                return (true, TimeSpan.Zero);
            }
        }

        return (true, TimeSpan.Zero);
    }
}
