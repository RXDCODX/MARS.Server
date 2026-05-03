using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._11_RandomMemReward;

public class RandomMem_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<RandomMem_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Random Mem";

    public override string AlertDescription { get; set; } = "Рандомный мем на экране";

    public override Color Color { get; set; } = Color.FromArgb(243, 255, 0);

    public override int Cost { get; init; } = 11;

    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
