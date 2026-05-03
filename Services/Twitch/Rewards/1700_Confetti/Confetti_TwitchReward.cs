using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._1700_Confetti;

public class Confetti_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Confetti_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Конфетти!";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(0, 255, 47);
    public override int Cost { get; init; } = 1700;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
