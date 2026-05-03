using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._10_RandomSound;

public class RandomSound_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<RandomSound_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Random sound";
    public override string AlertDescription { get; set; } = "Нажимать ради смешного момента";
    public override Color Color { get; set; } = Color.FromArgb(122, 167, 255);
    public override int Cost { get; init; } = 10;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
