namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public class AlertInitializationService : BackgroundService
{
    private readonly IServiceCollection _serviceCollection;

    public AlertInitializationService(IServiceCollection serviceCollection)
    {
        _serviceCollection = serviceCollection;
        InitializeRewards();
    }

    private void InitializeRewards()
    {
        var temporaryRewardTypes = typeof(TemporaryReward)
            .Assembly.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && typeof(TemporaryReward).IsAssignableFrom(type)
            )
            .OrderBy(type => type.FullName);

        foreach (var rewardType in temporaryRewardTypes)
        {
            _serviceCollection.AddSingleton(rewardType);
            _serviceCollection.AddSingleton(
                typeof(IHostedService),
                sp => (IHostedService)sp.GetRequiredService(rewardType)
            );
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
