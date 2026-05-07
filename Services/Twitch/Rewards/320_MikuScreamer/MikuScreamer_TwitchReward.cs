using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._320_MikuScreamer;

public class MikuScreamer_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<MikuScreamer_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "😱 MIKU SCREAMER";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(255, 0, 0);
    public override int Cost { get; init; } = 320;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
