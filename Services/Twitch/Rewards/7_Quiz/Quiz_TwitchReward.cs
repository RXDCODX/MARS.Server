using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._7_Quiz;

public class Quiz_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Quiz_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🧠 Викторина";
    public override string AlertDescription { get; set; } = "❓ Задает 1 вопрос в чат с подсказками";
    public override Color Color { get; set; } = Color.FromArgb(247, 0, 255);
    public override int Cost { get; init; } = 7;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
