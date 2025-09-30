using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public class AlertInitializationService(
    ChannelRewardsService channelRewardsService,
    ILogger<AlertInitializationService> logger,
    IDbContextFactory<AppDbContext> factory
) : BackgroundService
{
    public bool IsServiceActive { get; set; } = true;

    private const int AlertCost = 160;
    private const string AlertTitle = "НОГОЙ БОМЖА";
    private const string AlertPrompt =
        "Возращает баллы за использование если топтать asp/асп'a (аспиранта)";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            await InitializeAlertAsync();
        }

        // Ждем остановки сервиса
        await Task.CompletedTask;
    }

    /// <summary>
    /// Инициализирует алерт за 16 баллов, если он не существует.
    /// </summary>
    private async Task InitializeAlertAsync()
    {
        try
        {
            logger.LogInformation("Проверка существования алерта за {Cost} баллов", AlertCost);

            // Получаем все существующие награды
            var existingRewards = await channelRewardsService.GetRewardsAsync();
            if (existingRewards == null)
            {
                logger.LogError("Не удалось получить список наград канала");
                return;
            }

            // Проверяем, существует ли уже алерт за 16 баллов
            var existingAlert = existingRewards!.FirstOrDefault(r => r.Cost == AlertCost);
            if (existingAlert != null)
            {
                logger.LogInformation(
                    "Алерт за {Cost} баллов уже существует: {Title} (Id: {Id})",
                    AlertCost,
                    existingAlert.Title,
                    existingAlert.Id
                );
                return;
            }

            // Создаем новый алерт
            logger.LogInformation("Создание алерта за {Cost} баллов", AlertCost);

            var request = new CreateCustomRewardsRequest
            {
                Title = AlertTitle,
                Cost = AlertCost,
                IsEnabled = true,
                Prompt = AlertPrompt,
                BackgroundColor = "#FF0000",
                IsUserInputRequired = true,
                IsMaxPerStreamEnabled = false,
                IsMaxPerUserPerStreamEnabled = false,
                IsGlobalCooldownEnabled = false,
            };

            var rewardId = await channelRewardsService.CreateRewardAsync(request);
            if (rewardId != null)
            {
                logger.LogInformation(
                    "Алерт за {Cost} баллов успешно создан с Id: {RewardId}",
                    AlertCost,
                    rewardId
                );
                await using var dbContext = await factory.CreateDbContextAsync();
                var record = await dbContext.Alerts.SingleAsync(e =>
                    e.MetaInfo.TwitchPointsCost == AlertCost
                );
                record.MetaInfo.TwitchGuid = Guid.Parse(rewardId);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                logger.LogError("Не удалось создать алерт за {Cost} баллов", AlertCost);
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }
}
