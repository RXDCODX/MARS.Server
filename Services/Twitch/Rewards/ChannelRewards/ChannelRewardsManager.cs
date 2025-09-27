using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Entities;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Models;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

/// <summary>
/// Менеджер наград: CRUD + автопроверка существования и привязка к PyroAlerts (MediaInfo)
/// </summary>
public class ChannelRewardsManager(
    ChannelRewardsService channelRewardsService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<ChannelRewardsManager> logger
)
{
    /// <summary>
    /// Локальный CRUD: создаем/обновляем запись в БД (без немедленного вызова Twitch API).
    /// Синхронизация в Twitch выполняется отдельно (например, по расписанию/при старте).
    /// </summary>
    public async Task<ChannelRewardRecord?> UpsertLocalAsync(ChannelRewardRecord record)
    {
        ChannelRewardRecord? result = null;

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var existing = await db.Set<ChannelRewardRecord>()
                .FirstOrDefaultAsync(r => r.Cost == record.Cost || r.Title == record.Title);

            if (existing == null)
            {
                var rec = new ChannelRewardRecord
                {
                    Title = record.Title,
                    Cost = record.Cost,
                    IsEnabled = record.IsEnabled,
                    Prompt = record.Prompt,
                    BackgroundColor = record.BackgroundColor,
                    IsUserInputRequired = record.IsUserInputRequired,
                    IsMaxPerStreamEnabled = record.IsMaxPerStreamEnabled,
                    MaxPerStream = record.MaxPerStream,
                    IsMaxPerUserPerStreamEnabled = record.IsMaxPerUserPerStreamEnabled,
                    MaxPerUserPerStream = record.MaxPerUserPerStream,
                    IsGlobalCooldownEnabled = record.IsGlobalCooldownEnabled,
                    GlobalCooldownSeconds = record.GlobalCooldownSeconds,
                    ShouldRedemptionsSkipRequestQueue = record.ShouldRedemptionsSkipRequestQueue,
                    IsDeleted = false,
                    MediaInfoId = record.MediaInfoId,
                };
                db.Add(rec);
                await db.SaveChangesAsync();
                result = rec;
            }
            else
            {
                existing.Title = record.Title;
                existing.Cost = record.Cost;
                existing.IsEnabled = record.IsEnabled;
                existing.Prompt = record.Prompt;
                existing.BackgroundColor = record.BackgroundColor;
                existing.IsUserInputRequired = record.IsUserInputRequired;
                existing.IsMaxPerStreamEnabled = record.IsMaxPerStreamEnabled;
                existing.MaxPerStream = record.MaxPerStream;
                existing.IsMaxPerUserPerStreamEnabled = record.IsMaxPerUserPerStreamEnabled;
                existing.MaxPerUserPerStream = record.MaxPerUserPerStream;
                existing.IsGlobalCooldownEnabled = record.IsGlobalCooldownEnabled;
                existing.GlobalCooldownSeconds = record.GlobalCooldownSeconds;
                existing.ShouldRedemptionsSkipRequestQueue =
                    record.ShouldRedemptionsSkipRequestQueue;
                existing.IsDeleted = false;
                existing.MediaInfoId = record.MediaInfoId;
                db.Update(existing);
                await db.SaveChangesAsync();
                result = existing;
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        return result;
    }

    public async Task<IEnumerable<CustomReward>?> GetAllAsync()
    {
        return await channelRewardsService.GetRewardsAsync();
    }

    public async Task<List<ChannelRewardRecord>> GetLocalAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var list = await db.ChannelRewards.AsNoTracking().OrderBy(r => r.Title).ToListAsync();
        return list;
    }

    public async Task<ChannelRewardRecord?> GetLocalByIdAsync(Guid localId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var rec = await db.ChannelRewards.AsNoTracking().FirstOrDefaultAsync(r => r.Id == localId);
        return rec;
    }

    public async Task<CustomReward?> GetByIdAsync(string rewardId)
    {
        return await channelRewardsService.GetRewardByIdAsync(rewardId);
    }

    /// <summary>
    /// Мягкое удаление в локальной БД. Синхронизация удалит награду в Twitch позже.
    /// </summary>
    public async Task<bool> SoftDeleteLocalAsync(Guid localId)
    {
        var result = false;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var rec = await db.ChannelRewards.FirstOrDefaultAsync(e => e.Id == localId);
            if (rec != null)
            {
                rec.IsDeleted = true;
                db.Update(rec);
                await db.SaveChangesAsync();
                result = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        return result;
    }

    /// <summary>
    /// Обновление локальной записи (состояние). Синхронизация применит изменения к Twitch.
    /// </summary>
    public async Task<bool> UpdateLocalAsync(Guid localId, UpdateCustomRewardDto dto)
    {
        var result = false;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var rec = await db.ChannelRewards.FirstOrDefaultAsync(e => e.Id == localId);
            if (rec != null)
            {
                rec.Title = dto.Title ?? rec.Title;
                rec.Cost = dto.Cost ?? rec.Cost;
                rec.IsEnabled = dto.IsEnabled ?? rec.IsEnabled;
                rec.Prompt = dto.Prompt ?? rec.Prompt;
                rec.BackgroundColor = dto.BackgroundColor ?? rec.BackgroundColor;
                rec.IsUserInputRequired = dto.IsUserInputRequired ?? rec.IsUserInputRequired;
                rec.IsMaxPerStreamEnabled = dto.IsMaxPerStreamEnabled ?? rec.IsMaxPerStreamEnabled;
                rec.MaxPerStream = dto.MaxPerStream ?? rec.MaxPerStream;
                rec.IsMaxPerUserPerStreamEnabled =
                    dto.IsMaxPerUserPerStreamEnabled ?? rec.IsMaxPerUserPerStreamEnabled;
                rec.MaxPerUserPerStream = dto.MaxPerUserPerStream ?? rec.MaxPerUserPerStream;
                rec.IsGlobalCooldownEnabled =
                    dto.IsGlobalCooldownEnabled ?? rec.IsGlobalCooldownEnabled;
                rec.GlobalCooldownSeconds = dto.GlobalCooldownSeconds ?? rec.GlobalCooldownSeconds;
                rec.ShouldRedemptionsSkipRequestQueue =
                    dto.ShouldRedemptionsSkipRequestQueue ?? rec.ShouldRedemptionsSkipRequestQueue;

                db.Update(rec);
                await db.SaveChangesAsync();
                result = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        return result;
    }
}
