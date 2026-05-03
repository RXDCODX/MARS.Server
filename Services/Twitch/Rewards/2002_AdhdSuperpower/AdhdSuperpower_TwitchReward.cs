using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._2002_AdhdSuperpower;

public class AdhdSuperpower_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<AdhdSuperpower_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "ADHD SUPERPOWER";
    public override string AlertDescription { get; set; } = "Открывает на минуту ADHD макет, если уже есть на экране - добавляет минуту";
    public override Color Color { get; set; } = Color.FromArgb(16, 216, 144);
    public override int Cost { get; init; } = 2002;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
