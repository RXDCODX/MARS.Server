using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._341_Aga;

public class Aga_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Aga_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "AGA";
    public override string AlertDescription { get; set; } =
        "текст наверху и внизу можно разделить символом `=`";
    public override Color Color { get; set; } = Color.FromArgb(235, 4, 0);
    public override int Cost { get; init; } = 341;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
