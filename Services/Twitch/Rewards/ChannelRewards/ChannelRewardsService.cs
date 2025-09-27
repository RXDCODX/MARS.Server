using MARS.Server.Services.Twitch.Management;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public class ChannelRewardsService(
    ITwitchAPI api,
    TokenService tokenService,
    ILogger<ChannelRewardsService> logger
) : BackgroundService
{
    public bool IsServiceActive { get; set; } = true;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Создает награду канала. Возвращает идентификатор созданной награды.
    /// </summary>
    public async Task<string?> CreateRewardAsync(CreateCustomRewardsRequest request)
    {
        if (!IsServiceActive)
        {
            logger.LogWarning("ChannelRewardsService выключен");
            return null;
        }

        var accessToken = tokenService.Token?.AccessToken;
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        try
        {
            var response = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(
                TwitchExstension.ChannelId,
                request,
                accessToken
            );

            var created = response.Data.FirstOrDefault();
            if (created == null)
            {
                logger.LogError("Не удалось создать награду канала: пустой ответ");
                return null;
            }

            logger.LogInformation(
                "Создана награда канала: {Title} (Id: {Id}, Cost: {Cost})",
                created.Title,
                created.Id,
                created.Cost
            );

            return created.Id;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return null;
        }
    }

    /// <summary>
    /// Получает все награды канала.
    /// </summary>
    public async Task<IEnumerable<CustomReward>?> GetRewardsAsync()
    {
        if (!IsServiceActive)
        {
            logger.LogWarning("ChannelRewardsService выключен");
            return null;
        }

        var accessToken = tokenService.Token?.AccessToken;
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        try
        {
            var response = await api.Helix.ChannelPoints.GetCustomRewardAsync(
                TwitchExstension.ChannelId,
                null,
                false,
                accessToken
            );

            logger.LogInformation("Получено {Count} наград канала", response.Data.Length);
            return response.Data;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return null;
        }
    }

    /// <summary>
    /// Удаляет награду канала по её идентификатору.
    /// </summary>
    public async Task<bool> DeleteRewardAsync(string rewardId)
    {
        if (!IsServiceActive)
        {
            logger.LogWarning("ChannelRewardsService выключен");
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rewardId);

        var accessToken = tokenService.Token?.AccessToken;
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        try
        {
            await api.Helix.ChannelPoints.DeleteCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                accessToken
            );

            logger.LogInformation("Удалена награда канала: {RewardId}", rewardId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return false;
        }
    }

    /// <summary>
    /// Получить награду по идентификатору
    /// </summary>
    public async Task<CustomReward?> GetRewardByIdAsync(string rewardId)
    {
        if (!IsServiceActive)
        {
            logger.LogWarning("ChannelRewardsService выключен");
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rewardId);

        var accessToken = tokenService.Token?.AccessToken;
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        try
        {
            var response = await api.Helix.ChannelPoints.GetCustomRewardAsync(
                TwitchExstension.ChannelId,
                [rewardId],
                true,
                accessToken
            );

            return response.Data.FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return null;
        }
    }

    /// <summary>
    /// Обновить награду канала
    /// </summary>
    public async Task<bool> UpdateRewardAsync(string rewardId, UpdateCustomRewardRequest request)
    {
        if (!IsServiceActive)
        {
            logger.LogWarning("ChannelRewardsService выключен");
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rewardId);

        var accessToken = tokenService.Token?.AccessToken;
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        try
        {
            await api.Helix.ChannelPoints.UpdateCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                request,
                accessToken
            );

            logger.LogInformation("Обновлена награда канала: {RewardId}", rewardId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return false;
        }
    }
}
