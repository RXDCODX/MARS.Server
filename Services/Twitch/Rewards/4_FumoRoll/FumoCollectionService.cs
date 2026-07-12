using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Twitch.Rewards._4_FumoRoll;

public class FumoCollectionService(IDbContextFactory<AppDbContext> factory)
{
    private const int SameItemGuaranteeThreshold = 5;
    private const int UniqueItemGuaranteeThreshold = 20;

    public async Task<CollectionStats> RecordRollAsync(string twitchUserId, int fumoMfcId)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var existing = await dbContext.UserFumoCollections.FirstOrDefaultAsync(c =>
                c.TwitchUserId == twitchUserId && c.FumoMfcId == fumoMfcId
            );

            var isNew = existing is null;

            if (existing is not null)
            {
                existing.Count++;
                existing.LastObtained = DateTime.Now;
                dbContext.UserFumoCollections.Update(existing);
            }
            else
            {
                dbContext.UserFumoCollections.Add(
                    new UserFumoCollection
                    {
                        TwitchUserId = twitchUserId,
                        FumoMfcId = fumoMfcId,
                        Count = 1,
                        FirstObtained = DateTime.Now,
                        LastObtained = DateTime.Now,
                    }
                );
            }

            await dbContext.SaveChangesAsync();

            var collectedCount = await dbContext
                .UserFumoCollections.Where(c => c.TwitchUserId == twitchUserId)
                .Select(c => c.FumoMfcId)
                .Distinct()
                .CountAsync();

            var totalCount = await dbContext.Fumos.CountAsync();

            var thisModuleCount = existing?.Count + 1 ?? 1;

            var guaranteeTriggered = false;
            int? guaranteedPageId = null;

            if (
                thisModuleCount >= SameItemGuaranteeThreshold
                && thisModuleCount % SameItemGuaranteeThreshold == 0
            )
            {
                guaranteedPageId = await FindNewFumoAsync(dbContext, twitchUserId);
                guaranteeTriggered = guaranteedPageId is not null;
            }
            else if (
                collectedCount >= UniqueItemGuaranteeThreshold
                && collectedCount % UniqueItemGuaranteeThreshold == 0
            )
            {
                guaranteedPageId = await FindNewFumoAsync(dbContext, twitchUserId);
                guaranteeTriggered = guaranteedPageId is not null;
            }

            return new CollectionStats
            {
                CollectedCount = collectedCount,
                TotalCount = totalCount,
                ThisItemCount = thisModuleCount,
                IsNew = isNew,
                GuaranteeTriggered = guaranteeTriggered,
                GuaranteedItemId = guaranteedPageId,
            };
        }
        catch
        {
            return new CollectionStats
            {
                CollectedCount = 0,
                TotalCount = 0,
                ThisItemCount = 0,
                IsNew = false,
                GuaranteeTriggered = false,
                GuaranteedItemId = null,
            };
        }
    }

    public async Task<(int collected, int total)> GetUserCollectionStatsAsync(string twitchUserId)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var collected = await dbContext
                .UserFumoCollections.Where(c => c.TwitchUserId == twitchUserId)
                .Select(c => c.FumoMfcId)
                .Distinct()
                .CountAsync();

            var total = await dbContext.Fumos.CountAsync();

            return (collected, total);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static async Task<int?> FindNewFumoAsync(AppDbContext dbContext, string twitchUserId)
    {
        var ownedIds = await dbContext
            .UserFumoCollections.Where(c => c.TwitchUserId == twitchUserId)
            .Select(c => c.FumoMfcId)
            .Distinct()
            .ToListAsync();

        var newFumo = await dbContext
            .Fumos.Where(f => !ownedIds.Contains(f.MfcId))
            .OrderBy(f => f.LastOrder)
            .FirstOrDefaultAsync();

        return newFumo?.MfcId;
    }
}

public record CollectionStats
{
    public int CollectedCount { get; init; }
    public int TotalCount { get; init; }
    public int ThisItemCount { get; init; }
    public bool IsNew { get; init; }
    public bool GuaranteeTriggered { get; init; }
    public int? GuaranteedItemId { get; init; }
}
