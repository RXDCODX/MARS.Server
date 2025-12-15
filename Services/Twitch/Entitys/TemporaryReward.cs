using System.Timers;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Entitys;

public abstract class TemporaryReward(
    ChannelRewardsService channelRewardsService,
    ILogger<TemporaryReward> logger,
    IHostEnvironment environment
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

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Запуск временной награды: {AlertName} (Cost: {Cost})",
            AlertDisplayName,
            Cost
        );

        // Инициализация таймера с периодичностью 5 минут
        _timer = new Timer(TimeSpan.FromMinutes(5));
        OnTimerElapsed(this, new ElapsedEventArgs(DateTime.Now));
        _timer.Elapsed += OnTimerElapsed;

        return Task.CompletedTask;
    }

    public virtual async Task StopAsync(CancellationToken cancelToken)
    {
        logger.LogInformation("Остановка временной награды: {AlertName}", AlertDisplayName);

        // Останавливаем таймер
        if (_timer != null)
        {
            _timer?.Dispose();
            _timer = null;
        }

        // Удаляем награду если она существует
        await RemoveRewardIfExistsAsync();
    }

    private async void OnTimerElapsed(object? state, ElapsedEventArgs elapsedEventArgs)
    {
        if (environment.IsDevelopment())
        {
            return;
        }
        await _semaphore.WaitAsync();

        try
        {
            var now = elapsedEventArgs.SignalTime;
            var shouldBeEnabled = IsRewardEnabled(now);

            if (shouldBeEnabled)
            {
                // Награда должна быть доступна - проверяем через API и при необходимости создаём
                await EnsureRewardExistsAsync();
            }
            else
            {
                // Награда не должна быть доступна - удаляем если существует
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

    /// <summary>
    /// Убеждаемся, что награда существует. Если _rewardId есть - проверяем через API, иначе ищем/создаём
    /// </summary>
    private async Task EnsureRewardExistsAsync()
    {
        // Если _rewardId есть, проверяем что награда действительно существует
        if (!string.IsNullOrWhiteSpace(_rewardId))
        {
            var existingReward = await channelRewardsService.GetRewardByIdAsync(_rewardId);

            if (existingReward != null)
            {
                // Награда существует, всё хорошо
                return;
            }

            // Награда не найдена - очищаем кэш и ищем заново
            logger.LogWarning(
                "Награда {AlertName} с Id: {RewardId} не найдена на сервере. Ищем заново.",
                AlertDisplayName,
                _rewardId
            );
            _rewardId = null;
        }

        // Проверяем через API, не существует ли уже такая награда
        var existingRewards = await channelRewardsService.GetRewardsAsync();

        var duplicateReward = existingRewards?.FirstOrDefault(r =>
            r.Title.Equals(AlertDisplayName, StringComparison.OrdinalIgnoreCase) && r.Cost == Cost
        );

        if (duplicateReward != null)
        {
            logger.LogInformation(
                "Найдена существующая награда {AlertName} с Id: {RewardId}. Используем её.",
                AlertDisplayName,
                duplicateReward.Id
            );
            _rewardId = duplicateReward.Id;
            return;
        }

        // Награды нет - создаём
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
            logger.LogError("Не удалось создать временную награду: {AlertName}", AlertDisplayName);
        }
    }

    private async Task<bool> RemoveRewardIfExistsAsync()
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(_rewardId))
        {
            // Проверяем через API, существует ли награда на сервере Twitch
            var existingReward = await channelRewardsService.GetRewardByIdAsync(_rewardId);

            if (existingReward == null)
            {
                logger.LogWarning(
                    "Награда {AlertName} с Id: {RewardId} не найдена на сервере Twitch. Возможно, она уже была удалена.",
                    AlertDisplayName,
                    _rewardId
                );
                _rewardId = null;
                result = true;
            }
            else
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
        }

        return result;
    }

    private static string ColorToHex(Color color)
    {
        var result = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        return result;
    }
}
