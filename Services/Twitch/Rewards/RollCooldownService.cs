using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Twitch.Rewards;

public class RollCooldownService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<(bool allowed, TimeSpan remaining)> CheckAndUpdateCooldownAsync(
        string twitchUserId,
        string rollType,
        TimeSpan cooldown
    )
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var existing = await dbContext
                .RollCooldowns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.TwitchUserId == twitchUserId && r.RollType == rollType);

            if (existing is not null)
            {
                var elapsed = DateTime.Now - existing.LastRollTime;
                if (elapsed < cooldown)
                {
                    return (false, cooldown - elapsed);
                }
            }

            var record =
                existing ?? new RollCooldown { TwitchUserId = twitchUserId, RollType = rollType };

            record.LastRollTime = DateTime.Now;

            if (existing is not null)
            {
                dbContext.RollCooldowns.Update(record);
            }
            else
            {
                dbContext.RollCooldowns.Add(record);
            }

            await dbContext.SaveChangesAsync();

            return (true, TimeSpan.Zero);
        }
        catch
        {
            return (true, TimeSpan.Zero);
        }
    }
}
