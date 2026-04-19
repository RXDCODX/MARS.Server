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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
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

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenService.Token?.AccessToken);

        try
        {
            

            var response = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(
                TwitchExstension.ChannelId,
                request,
                tokenService.Token.AccessToken
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

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenService.Token?.AccessToken);

        try
        {
            var response = await api.Helix.ChannelPoints.GetCustomRewardAsync(
                TwitchExstension.ChannelId,
                null,
                false,
                tokenService.Token.AccessToken
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
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenService.Token?.AccessToken);

        try
        {
            await api.Helix.ChannelPoints.DeleteCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                tokenService.Token.AccessToken
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

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenService.Token?.AccessToken);

        try
        {
            var response = await api.Helix.ChannelPoints.GetCustomRewardAsync(
                TwitchExstension.ChannelId,
                [rewardId],
                true,
                tokenService.Token.AccessToken
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

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenService.Token?.AccessToken);

        try
        {
            await api.Helix.ChannelPoints.UpdateCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                request,
                tokenService.Token?.AccessToken
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
