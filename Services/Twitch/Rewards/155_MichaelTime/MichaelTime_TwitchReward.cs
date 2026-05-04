using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._155_MichaelTime;

public class MichaelTime_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<MichaelTime_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Michael Time!";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(255, 0, 0);
    public override int Cost { get; init; } = 155;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
