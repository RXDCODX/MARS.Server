using System.Timers;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

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

        // Награда должна сохраняться в системе, при остановке просто выключаем
        await EnsureRewardStateAsync(false);
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

            // Награда всегда должна существовать, по расписанию меняем только IsEnabled
            await EnsureRewardStateAsync(shouldBeEnabled);
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
    /// Убеждаемся, что награда существует, и приводим IsEnabled к нужному состоянию.
    /// Ищем по названию и цене (всегда уникально).
    /// </summary>
    private async Task EnsureRewardStateAsync(bool shouldBeEnabled)
    {
        // Проверяем через API, не существует ли уже такая награда
        var existingRewards = await channelRewardsService.GetRewardsAsync();

        var existingReward = existingRewards?.FirstOrDefault(r =>
            r.Title.Equals(AlertDisplayName, StringComparison.OrdinalIgnoreCase) && r.Cost == Cost
        );

        if (existingReward != null)
        {
            if (existingReward.IsEnabled != shouldBeEnabled)
            {
                var updated = await channelRewardsService.UpdateRewardAsync(
                    existingReward.Id,
                    new UpdateCustomRewardRequest { IsEnabled = shouldBeEnabled }
                );

                if (updated)
                {
                    logger.LogInformation(
                        "Награда {AlertName} (Cost: {Cost}) с Id: {RewardId} обновлена. IsEnabled={IsEnabled}.",
                        AlertDisplayName,
                        Cost,
                        existingReward.Id,
                        shouldBeEnabled
                    );
                }
                else
                {
                    logger.LogError(
                        "Не удалось обновить существующую награду {AlertName} (Id: {RewardId}).",
                        AlertDisplayName,
                        existingReward.Id
                    );
                }
            }
            else
            {
                logger.LogInformation(
                    "Награда {AlertName} (Cost: {Cost}) уже существует с Id: {RewardId} в нужном состоянии IsEnabled={IsEnabled}.",
                    AlertDisplayName,
                    Cost,
                    existingReward.Id,
                    shouldBeEnabled
                );
            }

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
        request.IsEnabled = shouldBeEnabled;
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
