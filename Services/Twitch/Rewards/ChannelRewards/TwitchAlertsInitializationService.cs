namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public static class TwitchAlertsInitializationService
{
    extension (IServiceCollection serviceCollection)
    {
        public IServiceCollection InitializeTwitchRewards(
        )
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
                serviceCollection.AddSingleton(rewardType);
                serviceCollection.AddSingleton(typeof(IHostedService), sp =>
                    (IHostedService)sp.GetRequiredService(rewardType)
                );
            }

            return serviceCollection;
        }
    }
}
