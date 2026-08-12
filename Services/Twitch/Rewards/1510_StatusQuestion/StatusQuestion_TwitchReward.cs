using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._1510_StatusQuestion;

public class StatusQuestion_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<StatusQuestion_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "❓ STATUS?";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(255, 0, 0);
    public override int Cost { get; init; } = 1510;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
