using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._8005_Credits;

public class Credits_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Credits_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "ТИТРЫ!!!";
    public override string AlertDescription { get; set; } = "Стример всех благодарит";
    public override Color Color { get; set; } = Color.FromArgb(123, 124, 255);
    public override int Cost { get; init; } = 8005;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
