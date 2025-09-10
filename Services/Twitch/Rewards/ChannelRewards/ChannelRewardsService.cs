using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Twitch.Management;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public class ChannelRewardsService(
    ITwitchAPI api,
    TokenService tokenService,
    ILogger<ChannelRewardsService> logger
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "channelrewards";
    public override string DisplayName => "Channel Rewards Management";
    public override string Description => "Создание и удаление наград канала Twitch";
    public override bool IsServiceActive { get; set; } = true;

    /// <summary>
    /// Создает награду канала. Возвращает идентификатор созданной награды.
    /// </summary>
    public async Task<string?> CreateRewardAsync(
        CreateCustomRewardsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsServiceActive)
        {
            logger.LogWarning("{Service} выключен", ServiceName);
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
    public async Task<IEnumerable<CustomReward>?> GetRewardsAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!IsServiceActive)
        {
            logger.LogWarning("{Service} выключен", ServiceName);
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
    public async Task<bool> DeleteRewardAsync(
        string rewardId,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsServiceActive)
        {
            logger.LogWarning("{Service} выключен", ServiceName);
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
}
