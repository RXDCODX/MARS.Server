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
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _runningTask;
    private readonly SemaphoreSlim _semaphore = new(1);

    private protected virtual CreateCustomRewardsRequest CreateCustomRewardsRequest =>
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

    public abstract string AlertDisplayName { get; set; }
    public abstract string AlertDescription { get; set; }
    public abstract Color Color { get; set; }
    public abstract int Cost { get; init; }
    public abstract Func<bool> IsRewardEnabled { get; set; }
    internal virtual Guid? TwitchRewardId { get; private set; }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Запуск временной награды: {AlertName} (Cost: {Cost})",
            AlertDisplayName,
            Cost
        );

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        _timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        if (environment.IsProduction())
        {
            await EnsureRewardStateAsync(IsRewardEnabled());
            _runningTask = RunTimerLoopAsync(_cancellationTokenSource.Token);
        }
    }

    public virtual async Task StopAsync(CancellationToken cancelToken)
    {
        logger.LogInformation("Остановка временной награды: {AlertName}", AlertDisplayName);

        _cancellationTokenSource?.Cancel();

        if (_timer != null)
        {
            _timer?.Dispose();
            _timer = null;
        }

        if (_runningTask != null)
        {
            try
            {
                await _runningTask.WaitAsync(cancelToken);
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }

            _runningTask = null;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        // Награда должна сохраняться в системе, при остановке просто выключаем
        await EnsureRewardStateAsync(false);
    }

    private async Task RunTimerLoopAsync(CancellationToken cancellationToken)
    {
        while (_timer != null && await _timer.WaitForNextTickAsync(cancellationToken))
        {
            await ExecuteRewardStateAsync(IsRewardEnabled(), cancellationToken);
        }
    }

    private async Task ExecuteRewardStateAsync(
        bool shouldBeEnabled,
        CancellationToken cancellationToken
    )
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
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

        var existingReward = existingRewards?.FirstOrDefault(r => r.Cost == Cost);

        if (existingReward != null)
        {
            TwitchRewardId = Guid.Parse(existingReward.Id);

            // Сравниваем текущие значения с желаемыми
            var desiredTitle = AlertDisplayName ?? string.Empty;
            var desiredPrompt = AlertDescription ?? string.Empty;
            var desiredCost = Cost;
            var desiredBg = ColorToHex(Color);

            var needUpdate = false;
            var updateRequest = new UpdateCustomRewardRequest { IsEnabled = shouldBeEnabled };

            if (!string.Equals(existingReward.Title ?? string.Empty, desiredTitle, StringComparison.Ordinal))
            {
                updateRequest.Title = desiredTitle;
                needUpdate = true;
            }

            if (!string.Equals(existingReward.Prompt ?? string.Empty, desiredPrompt, StringComparison.Ordinal))
            {
                updateRequest.Prompt = desiredPrompt;
                needUpdate = true;
            }

            if (existingReward.Cost != desiredCost)
            {
                updateRequest.Cost = desiredCost;
                needUpdate = true;
            }

            // BackgroundColor может быть null
            if (!string.Equals(existingReward.BackgroundColor ?? string.Empty, desiredBg, StringComparison.OrdinalIgnoreCase))
            {
                updateRequest.BackgroundColor = desiredBg;
                needUpdate = true;
            }

            // Если нужно обновить либо состояние IsEnabled, либо другие поля
            if (needUpdate || existingReward.IsEnabled != shouldBeEnabled)
            {
                var updated = await channelRewardsService.UpdateRewardAsync(existingReward.Id, updateRequest);

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
                    "Награда {AlertName} (Cost: {Cost}) уже существует с Id: {RewardId} в нужном состоянии и значениях.",
                    AlertDisplayName,
                    Cost,
                    existingReward.Id
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
            TwitchRewardId = Guid.Parse(rewardId);

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
        _ = ExecuteRewardStateAsync(IsRewardEnabled(), CancellationToken.None);
    }

    private static string ColorToHex(Color color)
    {
        var result = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        return result;
    }
}
