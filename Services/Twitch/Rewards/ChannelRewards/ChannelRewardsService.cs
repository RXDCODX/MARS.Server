using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Interfaces;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public class ChannelRewardsService : BackgroundService
{
    public IRewardsCacheService RewardsCacheService;
    public bool IsServiceActive { get; set; } = true;

    private readonly IOptionsMonitor<TwitchRewardsOptions> _rewardsOptionsMonitor;

    private readonly ITwitchAPI _api;
    private readonly TokenService _tokenService;
    private readonly ILogger<ChannelRewardsService> _logger;

    public ChannelRewardsService(
        ITwitchAPI api,
        TokenService tokenService,
        ILogger<ChannelRewardsService> logger,
        IOptionsMonitor<TwitchRewardsOptions> rewardsOptionsMonitor
    )
    {
        _api = api;
        _tokenService = tokenService;
        _logger = logger;
        _rewardsOptionsMonitor =
            rewardsOptionsMonitor ?? throw new ArgumentNullException(nameof(rewardsOptionsMonitor));
        RewardsCacheService = new RewardsCacheService(GetRewardsDirectAsync, logger);
    }

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
            _logger.LogWarning("ChannelRewardsService выключен");
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(_tokenService.Token?.AccessToken);

        try
        {
            var response = await _api.Helix.ChannelPoints.CreateCustomRewardsAsync(
                TwitchExstension.ChannelId,
                request,
                _tokenService.Token.AccessToken
            );

            var created = response.Data.FirstOrDefault();
            if (created == null)
            {
                _logger.LogError("Не удалось создать награду канала: пустой ответ");
                return null;
            }

            _logger.LogInformation(
                "Создана награда канала: {Title} (Id: {Id}, Cost: {Cost})",
                created.Title,
                created.Id,
                created.Cost
            );

            return created.Id;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
            return null;
        }
    }

    /// <summary>
    /// Получает все награды канала (с кешированием).
    /// </summary>
    public Task<IEnumerable<CustomReward>?> GetRewardsAsync() =>
        RewardsCacheService.GetRewardsAsync();

    /// <summary>
    /// Получает награды напрямую из API (без кеша).
    /// </summary>
    private async Task<IEnumerable<CustomReward>?> GetRewardsDirectAsync()
    {
        if (!IsServiceActive)
        {
            _logger.LogWarning("ChannelRewardsService выключен");
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(_tokenService.Token?.AccessToken);

        try
        {
            var response = await _api.Helix.ChannelPoints.GetCustomRewardAsync(
                TwitchExstension.ChannelId,
                null,
                false,
                _tokenService.Token.AccessToken
            );

            _logger.LogInformation("Получено {Count} наград канала", response.Data.Length);
            return response.Data;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
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
            _logger.LogWarning("ChannelRewardsService выключен");
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rewardId);
        ArgumentException.ThrowIfNullOrWhiteSpace(_tokenService.Token?.AccessToken);

        try
        {
            await _api.Helix.ChannelPoints.DeleteCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                _tokenService.Token.AccessToken
            );

            _logger.LogInformation("Удалена награда канала: {RewardId}", rewardId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
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
            _logger.LogWarning("ChannelRewardsService выключен");
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rewardId);

        ArgumentException.ThrowIfNullOrWhiteSpace(_tokenService.Token?.AccessToken);

        try
        {
            var response = await _api.Helix.ChannelPoints.GetCustomRewardAsync(
                TwitchExstension.ChannelId,
                [rewardId],
                true,
                _tokenService.Token.AccessToken
            );

            return response.Data.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
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
            _logger.LogWarning("ChannelRewardsService выключен");
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rewardId);

        ArgumentException.ThrowIfNullOrWhiteSpace(_tokenService.Token?.AccessToken);

        try
        {
            await _api.Helix.ChannelPoints.UpdateCustomRewardAsync(
                TwitchExstension.ChannelId,
                rewardId,
                request,
                _tokenService.Token?.AccessToken
            );

            _logger.LogInformation("Обновлена награда канала: {RewardId}", rewardId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
            return false;
        }
    }

    /// <summary>
    /// Возвращает переопределение включения награды по её цене, либо null если нет записи.
    /// </summary>
    public bool? GetEnabledOverrideForCost(int cost)
    {
        var dict = _rewardsOptionsMonitor.CurrentValue?.EnabledByCost;
        if (dict == null)
        {
            return null;
        }

        return dict.TryGetValue(cost, out var val) ? val : null;
    }
}
