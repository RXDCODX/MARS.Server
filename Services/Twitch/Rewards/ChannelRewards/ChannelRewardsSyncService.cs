using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Entities;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Models;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

/// <summary>
/// Сервис периодической синхронизации локальных записей наград с Twitch
/// </summary>
public class ChannelRewardsSyncService(
    ChannelRewardsService channelRewardsService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<ChannelRewardsSyncService> logger
) : BackgroundService
{
    public async Task SyncNow(CancellationToken cancellationToken = default)
    {
        await SyncOnce(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

    private async Task SyncOnce(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var local = await db.ChannelRewards.AsNoTracking().ToListAsync(cancellationToken);
        var remote = await channelRewardsService.GetRewardsAsync() ?? [];

        // Создание/обновление
        foreach (var record in local.Where(r => !r.IsDeleted))
        {
            var match = !string.IsNullOrWhiteSpace(record.TwitchRewardId)
                ? remote.FirstOrDefault(r => r.Id == record.TwitchRewardId)
                : remote.FirstOrDefault(r =>
                    r.Cost == record.Cost
                    || r.Title.Equals(record.Title, StringComparison.OrdinalIgnoreCase)
                );

            if (match == null)
            {
                // create
                var createReq = new CreateCustomRewardsRequest
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
                };

                var id = await channelRewardsService.CreateRewardAsync(createReq);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var tracked = await db.ChannelRewards.FirstAsync(
                        e => e.Id == record.Id,
                        cancellationToken
                    );
                    tracked.TwitchRewardId = id;
                    db.ChannelRewards.Update(tracked);
                    await db.SaveChangesAsync(cancellationToken);

                    // Привязка PyroAlerts по MediaInfoId или стоимости
                    await TryLinkPyroAlertAsync(db, record, id, cancellationToken);
                }
            }
            else
            {
                // update if differs
                var needsUpdate =
                    match.Cost != record.Cost
                    || !string.Equals(match.Title, record.Title, StringComparison.Ordinal);
                if (needsUpdate)
                {
                    var ok = await channelRewardsService.UpdateRewardAsync(
                        match.Id,
                        new TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest
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
                            ShouldRedemptionsSkipRequestQueue =
                                record.ShouldRedemptionsSkipRequestQueue,
                        }
                    );

                    if (ok && record.TwitchRewardId != match.Id)
                    {
                        var tracked = await db.ChannelRewards.FirstAsync(
                            e => e.Id == record.Id,
                            cancellationToken
                        );
                        tracked.TwitchRewardId = match.Id;
                        db.ChannelRewards.Update(tracked);
                        await db.SaveChangesAsync(cancellationToken);
                    }

                    // На всякий случай обеспечим привязку PyroAlerts
                    await TryLinkPyroAlertAsync(db, record, match.Id, cancellationToken);
                }
            }
        }

        // Удаление
        foreach (var record in local.Where(r => r.IsDeleted))
        {
            var match = !string.IsNullOrWhiteSpace(record.TwitchRewardId)
                ? remote.FirstOrDefault(r => r.Id == record.TwitchRewardId)
                : remote.FirstOrDefault(r =>
                    r.Cost == record.Cost
                    || r.Title.Equals(record.Title, StringComparison.OrdinalIgnoreCase)
                );

            if (match != null)
            {
                await channelRewardsService.DeleteRewardAsync(match.Id);
            }
        }
    }

    private async Task TryLinkPyroAlertAsync(
        AppDbContext db,
        ChannelRewardRecord record,
        string rewardId,
        CancellationToken ct
    )
    {
        try
        {
            if (record.MediaInfoId.HasValue)
            {
                var media = await db.Alerts.FirstOrDefaultAsync(
                    e => e.Id == record.MediaInfoId.Value,
                    ct
                );
                if (media != null)
                {
                    media.MetaInfo.TwitchGuid = Guid.Parse(rewardId);
                    db.Alerts.Update(media);
                    await db.SaveChangesAsync(ct);
                }
                return;
            }

            var byCost = await db
                .Alerts.AsNoTracking()
                .Where(a => a.MetaInfo.TwitchPointsCost == record.Cost)
                .ToListAsync(ct);

            if (byCost.Count == 1)
            {
                var media = byCost[0];
                media.MetaInfo.TwitchGuid = Guid.Parse(rewardId);
                db.Alerts.Update(media);
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }
}
