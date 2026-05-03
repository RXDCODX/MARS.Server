using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._1701_Fireworks;

public class Fireworks_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Fireworks_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Феерверк!";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(0, 255, 47);
    public override int Cost { get; init; } = 1701;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
