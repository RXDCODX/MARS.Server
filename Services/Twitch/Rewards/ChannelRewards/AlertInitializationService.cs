using MARS.Server.Services.ServiceManager;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public class AlertInitializationService(
    ChannelRewardsService channelRewardsService,
    ILogger<AlertInitializationService> logger
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "alertinitialization";
    public override string DisplayName => "Alert Initialization";
    public override string Description => "Инициализация алерта за 16 баллов канала";
    public override bool IsServiceActive { get; set; } = true;

    private const int AlertCost = 16;
    private const string AlertTitle = "Алерт";
    private const string AlertPrompt = "Введите текст для алерта";

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            await InitializeAlertAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Инициализирует алерт за 16 баллов, если он не существует.
    /// </summary>
    private async Task InitializeAlertAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Проверка существования алерта за {Cost} баллов", AlertCost);

            // Получаем все существующие награды
            var existingRewards = await channelRewardsService.GetRewardsAsync(cancellationToken);
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
                BackgroundColor = "#9146FF",
                IsUserInputRequired = true,
                IsMaxPerStreamEnabled = false,
                IsMaxPerUserPerStreamEnabled = false,
                IsGlobalCooldownEnabled = false,
            };

            var rewardId = await channelRewardsService.CreateRewardAsync(
                request,
                cancellationToken
            );
            if (rewardId != null)
            {
                logger.LogInformation(
                    "Алерт за {Cost} баллов успешно создан с Id: {RewardId}",
                    AlertCost,
                    rewardId
                );
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
