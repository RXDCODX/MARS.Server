using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._150_TyazheloReward;

public class Tyazhelo_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Tyazhelo_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Тяжело...";

    public override string AlertDescription { get; set; } = string.Empty;

    public override Color Color { get; set; } = Color.FromArgb(255, 0, 0);

    public override int Cost { get; init; } = 150;

    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
