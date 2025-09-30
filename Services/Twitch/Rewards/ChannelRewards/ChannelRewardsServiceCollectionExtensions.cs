using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Models;
using MARS.Server.Services.Twitch.Rewards.TestReward;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public static class ChannelRewardsServiceCollectionExtensions
{
    public static IServiceCollection AddChannelRewardsManager(this IServiceCollection services)
    {
        services.AddSingleton<ChannelRewardsService>();
        services.AddHostedService(sp => sp.GetRequiredService<ChannelRewardsService>());

        services.AddSingleton<ChannelRewardDefinition, TestRewardDefinition>();

        services.AddSingleton<ChannelRewardsManager>();
        services.AddSingleton<ChannelRewardsSyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<ChannelRewardsSyncService>());
        return services;
    }
}
