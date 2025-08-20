using MarchSeven.Models.HonkaiStarRail.Entitys;
using MarchSeven.Models.HonkaiStarRail.StarRailDailyNote;
using MarchSeven.Models.HoYoLab;
using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.Honkai.Entitys;
using MARS.Server.Services.Honkai.ManagedServices;
using MARS.Server.Services.Honkai.Repositories;
using MARS.Server.Services.Honkai.Services;

namespace MARS.Server.Services.Honkai;

/// <summary>
/// Методы расширения для регистрации сервисов Honkai: Star Rail в DI контейнере
/// </summary>
public static class HonkaiServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет все сервисы Honkai: Star Rail в коллекцию сервисов
    /// </summary>
    /// <param name="services">Коллекция сервисов DI контейнера</param>
    /// <returns>Коллекция сервисов для цепочки вызовов</returns>
    public static IServiceCollection AddHonkaiServices(this IServiceCollection services)
    {
        // Регистрируем конфигурацию мониторинга энергии
        services.AddSingleton<EnergyMonitoringConfiguration>();

        // Регистрируем репозитории (Data Access Layer)
        services.AddScoped<IHonkaiUserRepository, HonkaiUserRepository>();

        // Регистрируем бизнес-сервисы (Business Logic Layer)
        services.AddScoped<IHonkaiGameApiClient, HonkaiGameApiClient>();
        services.AddScoped<IHonkaiRewardService, HonkaiRewardService>();
        services.AddScoped<IHonkaiNotificationProvider, HonkaiNotificationProvider>();
        services.AddScoped<IHonkaiEnergyMonitor, HonkaiEnergyMonitor>();
        services.AddScoped<IHonkaiDailyMarkupProcessor, HonkaiDailyMarkupProcessor>();
        services.AddScoped<IHonkaiInitializationService, HonkaiInitializationService>();

        // Регистрируем управляемые сервисы (Background Services)
        services.AddSingleton<HonkaiDailyMarkRedeemManagedService>();
        services.AddHostedService(sp => sp.GetRequiredService<HonkaiDailyMarkRedeemManagedService>());

        services.AddSingleton<HonkaiEnergyNotificationManagedService>();
        services.AddHostedService(sp => sp.GetRequiredService<HonkaiEnergyNotificationManagedService>());

        // Для обратной совместимости регистрируем старые интерфейсы
        services.AddScoped<IHonkaiApiService>(sp => 
            new HonkaiApiServiceAdapter(sp.GetRequiredService<IHonkaiGameApiClient>(), sp.GetRequiredService<IHonkaiRewardService>()));
        services.AddScoped<IHonkaiNotificationService>(sp => 
            new HonkaiNotificationServiceAdapter(sp.GetRequiredService<IHonkaiNotificationProvider>()));

        return services;
    }
}

/// <summary>
/// Адаптер для обратной совместимости со старым интерфейсом IHonkaiApiService
/// </summary>
internal class HonkaiApiServiceAdapter : IHonkaiApiService
{
    private readonly IHonkaiGameApiClient _gameApiClient;
    private readonly IHonkaiRewardService _rewardService;

    public HonkaiApiServiceAdapter(IHonkaiGameApiClient gameApiClient, IHonkaiRewardService rewardService)
    {
        _gameApiClient = gameApiClient;
        _rewardService = rewardService;
    }

    public Task<StarRailUser?> GetStarRailUserAsync(DailyAutoMarkupUser user, HttpClient httpClient)
        => _gameApiClient.GetStarRailUserAsync(user, httpClient);

    public async Task<(bool Success, string? RewardName, int? Amount)> ClaimDailyRewardAsync(
        DailyAutoMarkupUser user, HttpClient httpClient)
    {
        var result = await _rewardService.ClaimDailyRewardAsync(user, httpClient);
        return (result.Success, result.RewardName, result.Amount);
    }

    public Task<StarRailDailyNote?> GetDailyNoteAsync(DailyAutoMarkupUser user, HttpClient httpClient)
        => _gameApiClient.GetDailyNoteAsync(user, httpClient);

    public Task<UserStatsData?> GetUserStatsAsync(DailyAutoMarkupUser user, HttpClient httpClient)
        => _gameApiClient.GetUserStatsAsync(user, httpClient);
}

/// <summary>
/// Адаптер для обратной совместимости со старым интерфейсом IHonkaiNotificationService
/// </summary>
internal class HonkaiNotificationServiceAdapter : IHonkaiNotificationService
{
    private readonly IHonkaiNotificationProvider _notificationProvider;

    public HonkaiNotificationServiceAdapter(IHonkaiNotificationProvider notificationProvider)
    {
        _notificationProvider = notificationProvider;
    }

    public Task SendMarkupFailureNotificationAsync(long telegramId, Guid userId)
        => _notificationProvider.SendMarkupFailureNotificationAsync(telegramId, userId);

    public Task SendMarkupSuccessNotificationAsync(long telegramId, string rewardName, int amount)
        => _notificationProvider.SendMarkupSuccessNotificationAsync(telegramId, rewardName, amount);

    public Task SendMarkupAlreadyReceivedNotificationAsync(long telegramId)
        => _notificationProvider.SendMarkupAlreadyReceivedNotificationAsync(telegramId);

    public Task SendEnergyNotificationAsync(
        DailyAutoMarkupUser user,
        int currentEnergy,
        int maxEnergy,
        int threshold,
        int gameUid,
        TimeSpan recoveryTime)
    {
        var notificationData = new EnergyNotificationData
        {
            User = user,
            CurrentEnergy = currentEnergy,
            MaxEnergy = maxEnergy,
            Threshold = threshold,
            GameUid = gameUid,
            RecoveryTime = recoveryTime
        };

        return _notificationProvider.SendEnergyNotificationAsync(notificationData);
    }
}
