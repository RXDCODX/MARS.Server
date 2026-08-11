using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Entities;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Models;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Api.Helix.Models.ChannelPoints;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

/// <summary>
/// Менеджер наград: CRUD + автопроверка существования и привязка к PyroAlerts (MediaInfo)
/// </summary>
public class ChannelRewardsManager(
    ChannelRewardsService channelRewardsService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<ChannelRewardsManager> logger,
    IServiceProvider serviceProvider
)
{
    /// <summary>
    /// Локальный CRUD: создаем/обновляем запись в БД (без немедленного вызова Twitch API).
    /// Синхронизация в Twitch выполняется отдельно (например, по расписанию/при старте).
    /// </summary>
    public async Task<ChannelRewardRecord?> UpsertLocalAsync(ChannelRewardDefinition definition)
    {
        ChannelRewardRecord? result = null;

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var existing = await db.Set<ChannelRewardRecord>()
                .FirstOrDefaultAsync(r => r.Cost == definition.Cost || r.Title == definition.Title);

            if (existing == null)
            {
                var rec = new ChannelRewardRecord
                {
                    Title = definition.Title,
                    Cost = definition.Cost,
                    IsEnabled = definition.IsEnabled,
                    Prompt = definition.Prompt,
                    BackgroundColor = definition.BackgroundColor,
                    IsUserInputRequired = definition.IsUserInputRequired,
                    IsMaxPerStreamEnabled = definition.IsMaxPerStreamEnabled,
                    MaxPerStream = definition.MaxPerStream,
                    IsMaxPerUserPerStreamEnabled = definition.IsMaxPerUserPerStreamEnabled,
                    MaxPerUserPerStream = definition.MaxPerUserPerStream,
                    IsGlobalCooldownEnabled = definition.IsGlobalCooldownEnabled,
                    GlobalCooldownSeconds = definition.GlobalCooldownSeconds,
                    ShouldRedemptionsSkipRequestQueue =
                        definition.ShouldRedemptionsSkipRequestQueue,
                    IsDeleted = false,
                    MediaInfoId = (definition as PyroAlertRewardDefinition)?.MediaInfoId,
                };
                db.Add(rec);
                await db.SaveChangesAsync();
                result = rec;
            }
            else
            {
                existing.Title = definition.Title;
                existing.Cost = definition.Cost;
                existing.IsEnabled = definition.IsEnabled;
                existing.Prompt = definition.Prompt;
                existing.BackgroundColor = definition.BackgroundColor;
                existing.IsUserInputRequired = definition.IsUserInputRequired;
                existing.IsMaxPerStreamEnabled = definition.IsMaxPerStreamEnabled;
                existing.MaxPerStream = definition.MaxPerStream;
                existing.IsMaxPerUserPerStreamEnabled = definition.IsMaxPerUserPerStreamEnabled;
                existing.MaxPerUserPerStream = definition.MaxPerUserPerStream;
                existing.IsGlobalCooldownEnabled = definition.IsGlobalCooldownEnabled;
                existing.GlobalCooldownSeconds = definition.GlobalCooldownSeconds;
                existing.ShouldRedemptionsSkipRequestQueue =
                    definition.ShouldRedemptionsSkipRequestQueue;
                existing.IsDeleted = false;
                existing.MediaInfoId = (definition as PyroAlertRewardDefinition)?.MediaInfoId;
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

    /// <summary>
    /// Собирает все сервисы, реализующие ITwitchReward, и создает записи в БД на их основе
    /// </summary>
    public async Task<int> SyncRewardServicesToLocalAsync()
    {
        var result = 0;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var existingLocal = await db.ChannelRewards.AsNoTracking().ToListAsync();

            // Получаем все зарегистрированные сервисы, реализующие ITwitchReward
            var rewardServices = serviceProvider.GetServices<ChannelRewardDefinition>();

            foreach (var rewardService in rewardServices)
            {
                // Проверяем, есть ли уже локальная запись с такой стоимостью
                var existing = existingLocal.FirstOrDefault(r => r.Cost == rewardService.Cost);
                if (existing != null)
                {
                    continue;
                }

                // Создаем новую локальную запись на основе сервиса
                var record = new ChannelRewardRecord
                {
                    Title = GetServiceName(rewardService),
                    Cost = rewardService.Cost,
                    IsEnabled = true,
                    Prompt = null,
                    BackgroundColor = "#9146FF",
                    IsUserInputRequired = false,
                    IsMaxPerStreamEnabled = false,
                    MaxPerStream = null,
                    IsMaxPerUserPerStreamEnabled = false,
                    MaxPerUserPerStream = null,
                    IsGlobalCooldownEnabled = false,
                    GlobalCooldownSeconds = null,
                    ShouldRedemptionsSkipRequestQueue = false,
                    IsDeleted = false,
                    TwitchRewardId = null, // Будет заполнено при синхронизации
                    MediaInfoId = null,
                };

                db.ChannelRewards.Add(record);
                result++;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        return result;
    }

    private static string GetServiceName(ITwitchReward rewardService)
    {
        var typeName = rewardService.GetType().Name;
        // Убираем суффиксы типа "Service", "Reward" и т.д.
        var cleanName = typeName.Replace("Service", "").Replace("Reward", "").Replace("Twitch", "");

        return cleanName;
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
