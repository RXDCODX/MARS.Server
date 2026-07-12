using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Twitch.Rewards._4_MikuModuleRoll;

public class MikuCollectionService(IDbContextFactory<AppDbContext> factory)
{
    private const int SameItemGuaranteeThreshold = 5;
    private const int UniqueItemGuaranteeThreshold = 20;

    public async Task<CollectionStats> RecordRollAsync(string twitchUserId, int mikuPageId)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var existing = await dbContext.UserMikuCollections.FirstOrDefaultAsync(c =>
                c.TwitchUserId == twitchUserId && c.MikuPageId == mikuPageId
            );

            var isNew = existing is null;

            if (existing is not null)
            {
                existing.Count++;
                existing.LastObtained = DateTime.Now;
                dbContext.UserMikuCollections.Update(existing);
            }
            else
            {
                dbContext.UserMikuCollections.Add(
                    new UserMikuCollection
                    {
                        TwitchUserId = twitchUserId,
                        MikuPageId = mikuPageId,
                        Count = 1,
                        FirstObtained = DateTime.Now,
                        LastObtained = DateTime.Now,
                    }
                );
            }

            await dbContext.SaveChangesAsync();

            var collectedCount = await dbContext
                .UserMikuCollections.Where(c => c.TwitchUserId == twitchUserId)
                .Select(c => c.MikuPageId)
                .Distinct()
                .CountAsync();

            var totalCount = await dbContext.MikuModules.CountAsync();

            var thisModuleCount = existing?.Count + 1 ?? 1;

            var guaranteeTriggered = false;
            int? guaranteedPageId = null;

            if (
                thisModuleCount >= SameItemGuaranteeThreshold
                && thisModuleCount % SameItemGuaranteeThreshold == 0
            )
            {
                guaranteedPageId = await FindNewModuleAsync(dbContext, twitchUserId);
                guaranteeTriggered = guaranteedPageId is not null;
            }
            else if (
                collectedCount >= UniqueItemGuaranteeThreshold
                && collectedCount % UniqueItemGuaranteeThreshold == 0
            )
            {
                guaranteedPageId = await FindNewModuleAsync(dbContext, twitchUserId);
                guaranteeTriggered = guaranteedPageId is not null;
            }

            return new CollectionStats
            {
                CollectedCount = collectedCount,
                TotalCount = totalCount,
                ThisModuleCount = thisModuleCount,
                IsNew = isNew,
                GuaranteeTriggered = guaranteeTriggered,
                GuaranteedPageId = guaranteedPageId,
            };
        }
        catch
        {
            return new CollectionStats
            {
                CollectedCount = 0,
                TotalCount = 0,
                ThisModuleCount = 0,
                IsNew = false,
                GuaranteeTriggered = false,
                GuaranteedPageId = null,
            };
        }
    }

    public async Task<(int collected, int total)> GetUserCollectionStatsAsync(string twitchUserId)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var collected = await dbContext
                .UserMikuCollections.Where(c => c.TwitchUserId == twitchUserId)
                .Select(c => c.MikuPageId)
                .Distinct()
                .CountAsync();

            var total = await dbContext.MikuModules.CountAsync();

            return (collected, total);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static async Task<int?> FindNewModuleAsync(AppDbContext dbContext, string twitchUserId)
    {
        var ownedIds = await dbContext
            .UserMikuCollections.Where(c => c.TwitchUserId == twitchUserId)
            .Select(c => c.MikuPageId)
            .Distinct()
            .ToListAsync();

        var newModule = await dbContext
            .MikuModules.Where(m => !ownedIds.Contains(m.PageId))
            .OrderBy(m => m.LastOrder)
            .FirstOrDefaultAsync();

        return newModule?.PageId;
    }
}

public record CollectionStats
{
    public int CollectedCount { get; init; }
    public int TotalCount { get; init; }
    public int ThisModuleCount { get; init; }
    public bool IsNew { get; init; }
    public bool GuaranteeTriggered { get; init; }
    public int? GuaranteedPageId { get; init; }
}
