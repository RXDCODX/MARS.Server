using System.Timers;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Entitys;

public abstract class TemporaryReward(
    ChannelRewardsService channelRewardsService,
    ILogger<TemporaryReward> logger
) : IHostedService, ITwitchReward
{
    private Timer? _timer;
    private string? _rewardId;
    private readonly SemaphoreSlim _semaphore = new(1);

    public abstract string AlertDisplayName { get; set; }
    public abstract string AlertDescription { get; set; }
    public abstract Color Color { get; set; }
    public abstract int Cost { get; init; }
    public abstract Func<DateTime, bool> IsRewardEnabled { get; set; }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Запуск временной награды: {AlertName} (Cost: {Cost})",
            AlertDisplayName,
            Cost
        );

        // Инициализация таймера с периодичностью 5 минут
        _timer = new Timer(TimeSpan.FromMinutes(5));
        _timer.Elapsed += OnTimerElapsed;

        await Task.CompletedTask;
    }

    public virtual async Task StopAsync(CancellationToken cancelToken)
    {
        logger.LogInformation("Остановка временной награды: {AlertName}", AlertDisplayName);

        // Останавливаем таймер
        if (_timer != null)
        {
            _timer.Dispose();
            _timer = null;
        }

        // Удаляем награду если она существует
        await RemoveRewardIfExistsAsync();
    }

    private async void OnTimerElapsed(object? state, ElapsedEventArgs elapsedEventArgs)
    {
        await _semaphore.WaitAsync();

        try
        {
            var now = DateTime.Now;
            var shouldBeEnabled = IsRewardEnabled(now);

            if (shouldBeEnabled && string.IsNullOrWhiteSpace(_rewardId))
            {
                // Награда должна быть доступна, но её нет - создаём
                await CreateRewardAsync();
            }
            else if (!shouldBeEnabled && !string.IsNullOrWhiteSpace(_rewardId))
            {
                // Награда не должна быть доступна, но она есть - удаляем
                await RemoveRewardIfExistsAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task CreateRewardAsync()
    {
        if (!string.IsNullOrWhiteSpace(_rewardId))
        {
            logger.LogWarning(
                "Попытка создать награду {AlertName}, но она уже существует с Id: {RewardId}",
                AlertDisplayName,
                _rewardId
            );
        }
        else
        {
            logger.LogInformation("Создание временной награды: {AlertName}", AlertDisplayName);

            var request = new CreateCustomRewardsRequest
            {
                Title = AlertDisplayName,
                Prompt = AlertDescription,
                Cost = Cost,
                BackgroundColor = ColorToHex(Color),
                IsEnabled = true,
                IsUserInputRequired = false,
                IsMaxPerStreamEnabled = false,
                IsMaxPerUserPerStreamEnabled = false,
                IsGlobalCooldownEnabled = false,
                ShouldRedemptionsSkipRequestQueue = false,
            };

            var rewardId = await channelRewardsService.CreateRewardAsync(request);

            if (!string.IsNullOrWhiteSpace(rewardId))
            {
                _rewardId = rewardId;
                logger.LogInformation(
                    "Временная награда {AlertName} успешно создана с Id: {RewardId}",
                    AlertDisplayName,
                    rewardId
                );
            }
            else
            {
                logger.LogError(
                    "Не удалось создать временную награду: {AlertName}",
                    AlertDisplayName
                );
            }
        }
    }

    private async Task<bool> RemoveRewardIfExistsAsync()
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(_rewardId))
        {
            logger.LogInformation(
                "Удаление временной награды: {AlertName} (Id: {RewardId})",
                AlertDisplayName,
                _rewardId
            );

            var deleted = await channelRewardsService.DeleteRewardAsync(_rewardId);

            if (deleted)
            {
                logger.LogInformation(
                    "Временная награда {AlertName} успешно удалена",
                    AlertDisplayName
                );
                _rewardId = null;
                result = true;
            }
            else
            {
                logger.LogError(
                    "Не удалось удалить временную награду {AlertName} с Id: {RewardId}",
                    AlertDisplayName,
                    _rewardId
                );
            }
        }

        return result;
    }

    private static string ColorToHex(Color color)
    {
        var result = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        return result;
    }
}
