using System.Timers;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Entitys;

public abstract class TemporaryReward(
    ChannelRewardsService channelRewardsService,
    ILogger logger,
    IHostEnvironment environment
) : IHostedService, ITwitchReward
{
    private Timer? _timer;
    private readonly SemaphoreSlim _semaphore = new(1);

    private protected virtual CreateCustomRewardsRequest CreateCustomRewardsRequest
    {
        get =>
            new()
            {
                Title = AlertDisplayName,
                Prompt = AlertDescription,
                Cost = Cost,
                IsEnabled = true,
                IsUserInputRequired = false,
                IsMaxPerStreamEnabled = false,
                IsMaxPerUserPerStreamEnabled = false,
                IsGlobalCooldownEnabled = false,
                ShouldRedemptionsSkipRequestQueue = false,
            };
    }

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
        if (!environment.IsProduction())
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
    /// Убеждаемся, что награда существует. Ищем по названию и цене (всегда уникально).
    /// </summary>
    private async Task EnsureRewardExistsAsync()
    {
        // Проверяем через API, не существует ли уже такая награда
        var existingRewards = await channelRewardsService.GetRewardsAsync();

        var existingReward = existingRewards?.FirstOrDefault(r =>
            r.Title.Equals(AlertDisplayName, StringComparison.OrdinalIgnoreCase) && r.Cost == Cost
        );

        if (existingReward != null)
        {
            logger.LogInformation(
                "Награда {AlertName} (Cost: {Cost}) уже существует с Id: {RewardId}. Используем её.",
                AlertDisplayName,
                Cost,
                existingReward.Id
            );
            return;
        }

        // Награды нет - создаём
        logger.LogInformation(
            "Создание временной награды: {AlertName} (Cost: {Cost})",
            AlertDisplayName,
            Cost
        );

        var request = CreateCustomRewardsRequest;
        request.BackgroundColor = ColorToHex(Color);
        request.Cost = Cost;
        request.Prompt = AlertDescription;
        request.Title = AlertDisplayName;

        var rewardId = await channelRewardsService.CreateRewardAsync(request);

        if (!string.IsNullOrWhiteSpace(rewardId))
        {
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
        // Ищем награду по названию и цене
        var existingRewards = await channelRewardsService.GetRewardsAsync();

        var rewardToDelete = existingRewards?.FirstOrDefault(r =>
            r.Title.Equals(AlertDisplayName, StringComparison.OrdinalIgnoreCase) && r.Cost == Cost
        );

        if (rewardToDelete == null)
        {
            logger.LogWarning(
                "Награда {AlertName} (Cost: {Cost}) не найдена на сервере Twitch. Возможно, она уже была удалена.",
                AlertDisplayName,
                Cost
            );
            return true;
        }

        logger.LogInformation(
            "Удаление временной награды: {AlertName} (Id: {RewardId})",
            AlertDisplayName,
            rewardToDelete.Id
        );

        var deleted = await channelRewardsService.DeleteRewardAsync(rewardToDelete.Id);

        if (deleted)
        {
            logger.LogInformation(
                "Временная награда {AlertName} успешно удалена",
                AlertDisplayName
            );
            return true;
        }

        logger.LogError(
            "Не удалось удалить временную награду {AlertName} с Id: {RewardId}",
            AlertDisplayName,
            rewardToDelete.Id
        );
        return false;
    }

    private protected void TimerElapseNow()
    {
        _timer?.Stop();
        OnTimerElapsed(this, new ElapsedEventArgs(DateTime.Now));
        _timer?.Start();
    }

    private static string ColorToHex(Color color)
    {
        var result = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        return result;
    }
}
