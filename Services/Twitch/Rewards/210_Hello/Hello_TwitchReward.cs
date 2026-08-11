using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._210_Hello;

public class Hello_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Hello_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "👋 Привет";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(0, 33, 255);
    public override int Cost { get; init; } = 210;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
