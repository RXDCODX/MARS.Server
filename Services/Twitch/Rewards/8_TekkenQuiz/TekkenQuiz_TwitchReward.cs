using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._8_TekkenQuiz;

public class TekkenQuiz_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<TekkenQuiz_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Теккен Викторина";
    public override string AlertDescription { get; set; } = "Leaderboard: !tekken_leaders YourStat: !tekken_me";
    public override Color Color { get; set; } = Color.FromArgb(247, 0, 255);
    public override int Cost { get; init; } = 8;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
